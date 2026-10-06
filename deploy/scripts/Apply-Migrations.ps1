# Applies the EF Core migrations to Azure SQL.
#  1. Waits for the serverless database to wake up (it auto-pauses after an hour idle).
#  2. Runs the one-time baseline (marks the hand-built tables as InitialBaseline).
#  3. Runs the idempotent migration script, which only applies migrations that are missing.
#  4. Prints the migration history and checks the newest index exists.
# Firewall: the server has "Allow Azure services and resources to access this server" on,
# so the Microsoft-hosted agent can connect without adding its own IP rule.
param(
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [Parameter(Mandatory)] [string] $WebAppName,
    [Parameter(Mandatory)] [string] $SqlFolder,
    [int] $WakeAttempts = 10,
    [int] $WakeDelaySeconds = 30
)
$ErrorActionPreference = 'Stop'

if (-not (Get-Module -ListAvailable -Name SqlServer)) {
    Write-Host 'Installing the SqlServer PowerShell module...'
    Install-Module SqlServer -Scope CurrentUser -Force -AllowClobber
}
Import-Module SqlServer

$cs = & "$PSScriptRoot/Get-SqlConnectionString.ps1" -ResourceGroup $ResourceGroup -WebAppName $WebAppName

# Serverless Azure SQL can take a minute to resume, so retry the first connection.
for ($i = 1; $i -le $WakeAttempts; $i++) {
    try {
        $row = Invoke-Sqlcmd -ConnectionString $cs -Query 'SELECT DB_NAME() AS Db, SYSUTCDATETIME() AS UtcNow' -QueryTimeout 60
        Write-Host "Connected to $($row.Db) on attempt $i."
        break
    }
    catch {
        if ($i -eq $WakeAttempts) { throw "Database did not wake up after $WakeAttempts attempts: $($_.Exception.Message)" }
        Write-Host "Attempt $i failed (database probably resuming). Waiting $WakeDelaySeconds s..."
        Start-Sleep -Seconds $WakeDelaySeconds
    }
}

function Show-History([string] $Label) {
    $exists = Invoke-Sqlcmd -ConnectionString $cs -Query "SELECT CASE WHEN OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL THEN 0 ELSE 1 END AS HasTable"
    if ($exists.HasTable -eq 0) { Write-Host "${Label}: no __EFMigrationsHistory table yet."; return }
    Write-Host "${Label}:"
    Invoke-Sqlcmd -ConnectionString $cs -Query 'SELECT MigrationId, ProductVersion FROM [__EFMigrationsHistory] ORDER BY MigrationId' |
        Format-Table -AutoSize | Out-String | Write-Host
}

Show-History 'Migration history before deploy'

Write-Host 'Running baseline-existing-database.sql'
Invoke-Sqlcmd -ConnectionString $cs -InputFile (Join-Path $SqlFolder 'baseline-existing-database.sql') -Verbose

Write-Host 'Running migrate-idempotent.sql'
Invoke-Sqlcmd -ConnectionString $cs -InputFile (Join-Path $SqlFolder 'migrate-idempotent.sql') -QueryTimeout 600 -Verbose

Show-History 'Migration history after deploy'

$index = Invoke-Sqlcmd -ConnectionString $cs -Query "SELECT name FROM sys.indexes WHERE name = 'IX_Donations_UserId_DonationDate' AND object_id = OBJECT_ID('dbo.Donations')"
if (-not $index) { throw 'Index IX_Donations_UserId_DonationDate is missing after the migration.' }
Write-Host "Schema check passed: $($index.name) exists on dbo.Donations."
