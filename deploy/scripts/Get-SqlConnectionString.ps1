# Reads the SQL connection string from the web app's app settings at run time.
# The value lives only in Azure App Service settings, never in the repo or in pipeline variables.
# It is registered as a secret straight away, so Azure DevOps masks it if it ever reaches a log.
param(
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [Parameter(Mandatory)] [string] $WebAppName
)

$cs = az webapp config appsettings list -g $ResourceGroup -n $WebAppName `
    --query "[?name=='ConnectionStrings__DefaultConnection'].value | [0]" -o tsv

if ([string]::IsNullOrWhiteSpace($cs)) {
    throw "App setting ConnectionStrings__DefaultConnection is missing on $WebAppName."
}

if ($env:TF_BUILD) { Write-Host "##vso[task.setsecret]$cs" }
return $cs
