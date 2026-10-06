# Checks that the app settings an app needs are present in Azure, without printing any values.
# Secrets such as the SQL connection string are managed in App Service settings, not in the repo.
param(
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [Parameter(Mandatory)] [string] $AppName,
    [Parameter(Mandatory)] [string[]] $Required
)
$ErrorActionPreference = 'Stop'

$names = az webapp config appsettings list -g $ResourceGroup -n $AppName --query '[].name' -o tsv
$missing = $Required | Where-Object { $names -notcontains $_ }

foreach ($name in $Required) {
    $state = if ($names -contains $name) { 'present' } else { 'MISSING' }
    Write-Host ("{0,-40} {1}" -f $name, $state)
}

if ($missing) { throw "Missing app settings on ${AppName}: $($missing -join ', ')" }
Write-Host "All $($Required.Count) required app settings are present on $AppName (values not shown)."
