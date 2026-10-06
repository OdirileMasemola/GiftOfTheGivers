# Post-deploy smoke test for the web app: the home page must return HTTP 200 and the footer
# must show the build number that was just deployed. Retries while the app restarts.
param(
    [Parameter(Mandatory)] [string] $Url,
    [string] $ExpectedBuild = '',
    [int] $Attempts = 12,
    [int] $DelaySeconds = 15
)
$ErrorActionPreference = 'Stop'

for ($i = 1; $i -le $Attempts; $i++) {
    try {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 60 -Headers @{ 'Cache-Control' = 'no-cache' }
        $sw.Stop()
        $build = if ($response.Content -match 'footer-build[^>]*>\s*Build\s+([^<\s]+)') { $Matches[1] } else { '(not found)' }
        Write-Host "Attempt ${i}: HTTP $($response.StatusCode) in $($sw.ElapsedMilliseconds) ms, footer build = $build"

        if ($response.StatusCode -eq 200 -and ([string]::IsNullOrEmpty($ExpectedBuild) -or $build -eq $ExpectedBuild)) {
            Write-Host "Smoke test passed for $Url"
            return
        }
    }
    catch {
        Write-Host "Attempt ${i}: $($_.Exception.Message)"
    }
    Start-Sleep -Seconds $DelaySeconds
}
throw "Smoke test failed: $Url did not return HTTP 200 with build '$ExpectedBuild'."
