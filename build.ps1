$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot

$appConstantsPath = Join-Path $repoRoot "HotspotApp\Constants\AppConstants.cs"
if (-not (Test-Path $appConstantsPath)) {
    throw "AppConstants.cs not found at $appConstantsPath"
}
$appConstantsContent = Get-Content -Raw -Path $appConstantsPath

$appNameMatch = [regex]::Match($appConstantsContent, 'AppName\s*=\s*"([^"]+)"')
if (-not $appNameMatch.Success) {
    throw "AppName not found in AppConstants.cs"
}
$appName = $appNameMatch.Groups[1].Value

$versionMatch = [regex]::Match($appConstantsContent, 'Version\s*=\s*"([^"]+)"')
if (-not $versionMatch.Success) {
    throw "Version not found in AppConstants.cs"
}
$version = $versionMatch.Groups[1].Value

$authorMatch = [regex]::Match($appConstantsContent, 'Author\s*=\s*"([^"]+)"')
if (-not $authorMatch.Success) {
    throw "Author not found in AppConstants.cs"
}
$author = $authorMatch.Groups[1].Value

$uiProjectPath = Join-Path $repoRoot "HotspotApp\HotspotApp.csproj"
$publishDir = Join-Path $repoRoot "Publish"
$tempPublishDir = Join-Path $publishDir ".tmp"

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force -Path $publishDir
}

dotnet publish $uiProjectPath -c Release -r win-x64 -o $tempPublishDir `
    -p:AssemblyTitle=$appName `
    -p:Product=$appName `
    -p:Company=$author `
    -p:Copyright=$author `
    -p:Version=$version `
    -p:AssemblyVersion=$version `
    -p:FileVersion=$version `
    -p:InformationalVersion=$version

if (-not (Test-Path $tempPublishDir)) {
    throw "Build failed"
}
$publishedExe = Get-ChildItem -Path $tempPublishDir -Filter "*.exe" -File | Select-Object -First 1
if (-not $publishedExe) {
    throw "No executable was generated in $tempPublishDir"
}

if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir | Out-Null
}

$finalExePath = Join-Path $publishDir "$appName.exe"
Move-Item -Path $publishedExe.FullName -Destination $finalExePath -Force

Remove-Item -Recurse -Force -Path $tempPublishDir
