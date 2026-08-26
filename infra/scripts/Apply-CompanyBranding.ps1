[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OrganizationId,

    [string]$TenantId = "",

    [string]$BaseUrl = "https://woodgrovedemo.com",

    [string]$BrandAssetsBaseUrl = "",

    [string]$BrandingAssetRoot = ""
)

$ErrorActionPreference = "Stop"

function Get-GraphAccessToken {
    param([string]$Tenant)

    $tenantArgs = @()
    if (-not [string]::IsNullOrWhiteSpace($Tenant)) {
        $tenantArgs = @("--tenant", $Tenant)
    }

    $token = az account get-access-token `
        --resource-type ms-graph `
        --query accessToken `
        -o tsv `
        @tenantArgs

    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Unable to acquire a Microsoft Graph access token. Run 'az login' against the target tenant first."
    }

    return $token.Trim()
}

function Invoke-GraphJsonPatch {
    param(
        [string]$Uri,
        [hashtable]$Headers,
        [hashtable]$Body
    )

    $json = $Body | ConvertTo-Json -Depth 8
    Invoke-RestMethod -Method Patch -Uri $Uri -Headers $Headers -Body $json -ContentType "application/json; charset=utf-8" | Out-Null
}

function Invoke-GraphStreamUpload {
    param(
        [string]$Uri,
        [hashtable]$Headers,
        [string]$Path,
        [string]$ContentType
    )

    if (-not (Test-Path $Path)) {
        throw "Branding asset not found: $Path"
    }

    Invoke-RestMethod -Method Put -Uri $Uri -Headers $Headers -InFile $Path -ContentType $ContentType | Out-Null
}

function Get-NormalizedBaseUrl {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    return $Value.Trim().TrimEnd('/')
}

function Resolve-BrandingAssetPath {
    param(
        [string]$FileName,
        [string]$LocalRoot,
        [string]$RemoteBaseUrl,
        [string]$ScratchRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($LocalRoot)) {
        $localPath = Join-Path $LocalRoot $FileName
        if (Test-Path $localPath) {
            return $localPath
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($RemoteBaseUrl)) {
        if (-not (Test-Path $ScratchRoot)) {
            New-Item -ItemType Directory -Path $ScratchRoot -Force | Out-Null
        }

        $downloadPath = Join-Path $ScratchRoot $FileName
        Invoke-WebRequest -Uri "$RemoteBaseUrl/$FileName" -OutFile $downloadPath | Out-Null
        return $downloadPath
    }

    throw "Unable to resolve $FileName. Provide -BrandingAssetRoot or -BrandAssetsBaseUrl (or set BRAND_ASSETS_BASE_URL / BrandAssets__BaseUrl)."
}

$repoAssetRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..\\src\\storefront\\wwwroot\\Company-branding")).Path
$assetRoot = ""
if (-not [string]::IsNullOrWhiteSpace($BrandingAssetRoot)) {
    $assetRoot = (Resolve-Path $BrandingAssetRoot).Path
}

$resolvedBrandAssetsBaseUrl = Get-NormalizedBaseUrl $BrandAssetsBaseUrl
if ([string]::IsNullOrWhiteSpace($resolvedBrandAssetsBaseUrl)) {
    $resolvedBrandAssetsBaseUrl = Get-NormalizedBaseUrl $env:BRAND_ASSETS_BASE_URL
}
if ([string]::IsNullOrWhiteSpace($resolvedBrandAssetsBaseUrl)) {
    $resolvedBrandAssetsBaseUrl = Get-NormalizedBaseUrl $env:BrandAssets__BaseUrl
}

$scratchRoot = Join-Path $PSScriptRoot ".generated\\company-branding"
$loginTextPath = Join-Path $repoAssetRoot "login-text-en.md"
$signInPageText = (Get-Content $loginTextPath -Raw).Trim()
$token = Get-GraphAccessToken -Tenant $TenantId
$headers = @{
    Authorization = "Bearer $token"
    "Accept-Language" = "0"
}

$brandingUri = "https://graph.microsoft.com/v1.0/organization/$OrganizationId/branding"

$body = @{
    backgroundColor = "#343434"
    headerBackgroundColor = "#223846"
    signInPageText = $signInPageText
    usernameHintText = "Email address"
    customPrivacyAndCookiesText = "Privacy & Cookies statement"
    customPrivacyAndCookiesUrl = "$BaseUrl/privacy"
    customTermsOfUseText = "Woodgrove terms of use"
    customTermsOfUseUrl = "$BaseUrl/tos"
    loginPageLayoutConfiguration = @{
        layoutTemplateType = "default"
        isHeaderShown = $true
        isFooterShown = $true
    }
    loginPageTextVisibilitySettings = @{
        hideCannotAccessYourAccount = $false
        hideAccountResetCredentials = $false
        hideTermsOfUse = $false
        hidePrivacyAndCookies = $false
        hideForgotMyPassword = $false
        hideResetItNow = $false
    }
}

Invoke-GraphJsonPatch -Uri $brandingUri -Headers $headers -Body $body

$uploads = @(
    @{ Name = "bannerLogo"; FileName = "af-bannerlogo.png"; ContentType = "image/png" }
    @{ Name = "headerLogo"; FileName = "af-headerlogo.png"; ContentType = "image/png" }
    @{ Name = "backgroundImage"; FileName = "af-background.jpg"; ContentType = "image/jpeg" }
    @{ Name = "favicon"; FileName = "af-favicon.png"; ContentType = "image/png" }
    @{ Name = "customCSS"; Path = (Join-Path $repoAssetRoot "af-custom.css"); ContentType = "text/css" }
    @{ Name = "squareLogo"; FileName = "af-square-logo-light.png"; ContentType = "image/png" }
    @{ Name = "squareLogoDark"; FileName = "af-square-logo-dark.png"; ContentType = "image/png" }
)

try {
    foreach ($upload in $uploads) {
        $resolvedPath = $upload.Path
        if ($upload.ContainsKey("FileName")) {
            $resolvedPath = Resolve-BrandingAssetPath `
                -FileName $upload.FileName `
                -LocalRoot $assetRoot `
                -RemoteBaseUrl $resolvedBrandAssetsBaseUrl `
                -ScratchRoot $scratchRoot
        }

        Invoke-GraphStreamUpload `
            -Uri "$brandingUri/$($upload.Name)" `
            -Headers $headers `
            -Path $resolvedPath `
            -ContentType $upload.ContentType
    }
}
finally {
    if (Test-Path $scratchRoot) {
        Remove-Item -Path $scratchRoot -Recurse -Force
    }
}

Write-Host "Applied default company branding assets and text for organization $OrganizationId."
Write-Host "Remaining live-tenant tasks: enable the custom URL domain in Entra/Front Door and confirm the SSPR branding toggle in the Entra admin center."
