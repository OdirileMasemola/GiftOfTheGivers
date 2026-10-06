# Post-deploy smoke test for GenerateTaxCertificate.
# The function key is fetched from Azure at run time and masked; it is never stored or printed.
#  1. Without the key the function must refuse the call (HTTP 401).
#  2. With the key it must return the tax certificate for a known completed donation (HTTP 200).
#     The function returns the existing certificate if there is one, so this does not add data.
param(
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [Parameter(Mandatory)] [string] $FunctionAppName,
    [Parameter(Mandatory)] [string] $BaseUrl,
    [Parameter(Mandatory)] [int] $DonationId,
    [int] $Attempts = 10,
    [int] $DelaySeconds = 20
)
$ErrorActionPreference = 'Stop'

$key = az functionapp function keys list -g $ResourceGroup -n $FunctionAppName `
    --function-name GenerateTaxCertificate --query default -o tsv
if ([string]::IsNullOrWhiteSpace($key)) { throw 'Could not read the GenerateTaxCertificate function key.' }
if ($env:TF_BUILD) { Write-Host "##vso[task.setsecret]$key" }

$endpoint = "$BaseUrl/api/GenerateTaxCertificate/$DonationId"

# 1. No key: expect 401.
$noKeyStatus = 0
for ($i = 1; $i -le $Attempts; $i++) {
    try {
        Invoke-WebRequest -Uri $endpoint -UseBasicParsing -TimeoutSec 90 | Out-Null
        $noKeyStatus = 200
    }
    catch {
        $noKeyStatus = [int]$_.Exception.Response.StatusCode
    }
    if ($noKeyStatus -eq 401) { break }
    Write-Host "Attempt ${i}: call without key returned $noKeyStatus, retrying while the app starts..."
    Start-Sleep -Seconds $DelaySeconds
}
if ($noKeyStatus -ne 401) { throw "Expected HTTP 401 without the function key, got $noKeyStatus." }
Write-Host 'Call without the function key was refused with HTTP 401, as expected.'

# 2. With key: expect 200 and a certificate number.
for ($i = 1; $i -le $Attempts; $i++) {
    try {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $result = Invoke-RestMethod -Uri $endpoint -Headers @{ 'x-functions-key' = $key } -TimeoutSec 90
        $sw.Stop()
        $number = if ($result.certificateNumber) { $result.certificateNumber } else { $result.certificate.certificateNumber }
        if ($number) {
            Write-Host "Attempt ${i}: HTTP 200 in $($sw.ElapsedMilliseconds) ms. Donation $DonationId -> certificate $number"
            Write-Host 'Function smoke test passed.'
            return
        }
        Write-Host "Attempt ${i}: HTTP 200 but no certificate number in the response."
    }
    catch {
        Write-Host "Attempt ${i}: $($_.Exception.Message)"
    }
    Start-Sleep -Seconds $DelaySeconds
}
throw 'Function smoke test failed.'
