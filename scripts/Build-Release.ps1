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
$buildId = [Guid]::NewGuid().ToString('N')
$stagingRoot = Join-Path $releaseRoot '.staging'
$stagingDirectory = Join-Path $stagingRoot $buildId
$stagingArchive = Join-Path $stagingRoot "$buildId.zip"
$backupDirectory = Join-Path $releaseRoot ".backup-$buildId"

& (Join-Path $PSScriptRoot 'Get-ZapretEngine.ps1')

New-Item -ItemType Directory -Force -Path $releaseRoot, $stagingRoot | Out-Null
$resolvedRelease = [IO.Path]::GetFullPath($releaseRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
foreach ($candidate in @($publishDirectory, $stagingDirectory, $stagingArchive, $backupDirectory)) {
    $resolvedCandidate = [IO.Path]::GetFullPath($candidate)
    if (-not $resolvedCandidate.StartsWith($resolvedRelease + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Güvenli olmayan yayın yolu: $resolvedCandidate"
    }
}

try {
    & $dotnet test (Join-Path $repositoryRoot 'ZapretTR.sln') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Testler başarısız oldu.' }

    & $dotnet publish (Join-Path $repositoryRoot 'src\ZapretTR.App\ZapretTR.App.csproj') `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $stagingDirectory `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None
    if ($LASTEXITCODE -ne 0) { throw 'Yayın derlemesi başarısız oldu.' }

    $requiredOutputs = @(
        'ZapretTR.exe',
        'engine\winws2.exe',
        'engine\cygwin1.dll',
        'engine\WinDivert.dll',
        'engine\WinDivert64.sys',
        'engine\zapret-lib.lua',
        'profiles\tr\balanced.json'
    )
    foreach ($relativePath in $requiredOutputs) {
        if (-not (Test-Path -LiteralPath (Join-Path $stagingDirectory $relativePath))) {
            throw "Yayın çıktısı eksik: $relativePath"
        }
    }

    Compress-Archive -Path (Join-Path $stagingDirectory '*') -DestinationPath $stagingArchive -CompressionLevel Optimal

    if (Test-Path -LiteralPath $publishDirectory) {
        Move-Item -LiteralPath $publishDirectory -Destination $backupDirectory
    }

    try {
        Move-Item -LiteralPath $stagingDirectory -Destination $publishDirectory
    }
    catch {
        if (Test-Path -LiteralPath $backupDirectory) {
            Move-Item -LiteralPath $backupDirectory -Destination $publishDirectory
        }
        throw
    }

    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -Force -LiteralPath $archivePath
    }
    Move-Item -LiteralPath $stagingArchive -Destination $archivePath

    if (Test-Path -LiteralPath $backupDirectory) {
        Remove-Item -Recurse -Force -LiteralPath $backupDirectory
    }
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -Recurse -Force -LiteralPath $stagingDirectory
    }
    if (Test-Path -LiteralPath $stagingArchive) {
        Remove-Item -Force -LiteralPath $stagingArchive
    }
}

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()

Write-Host "Yayın: $archivePath"
Write-Host "SHA-256: $hash"
