[CmdletBinding()]
param(
    [string]$Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$localDotnet = Join-Path $repositoryRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$releaseRoot = Join-Path $repositoryRoot 'artifacts\release'
$publishDirectory = Join-Path $releaseRoot "ZapretTR-v$Version-win-x64"
$archivePath = Join-Path $releaseRoot "ZapretTR-v$Version-win-x64.zip"

& (Join-Path $PSScriptRoot 'Get-ZapretEngine.ps1')

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
if (Test-Path -LiteralPath $publishDirectory) {
    $resolvedRelease = [IO.Path]::GetFullPath($releaseRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolvedPublish = [IO.Path]::GetFullPath($publishDirectory)
    if (-not $resolvedPublish.StartsWith($resolvedRelease + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Güvenli olmayan yayın yolu: $resolvedPublish"
    }
    Remove-Item -Recurse -Force -LiteralPath $resolvedPublish
}

& $dotnet test (Join-Path $repositoryRoot 'ZapretTR.sln') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Testler başarısız oldu.' }

& $dotnet publish (Join-Path $repositoryRoot 'src\ZapretTR.App\ZapretTR.App.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) { throw 'Yayın derlemesi başarısız oldu.' }

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -Force -LiteralPath $archivePath
}
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()

Write-Host "Yayın: $archivePath"
Write-Host "SHA-256: $hash"
