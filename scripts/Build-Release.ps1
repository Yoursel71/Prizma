[CmdletBinding()]
param(
    [string]$Version = '1.0.1'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$localDotnet = Join-Path $repositoryRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$releaseRoot = Join-Path $repositoryRoot 'artifacts\release'
$publishDirectory = Join-Path $releaseRoot "Prizma-v$Version-win-x64"
$archivePath = Join-Path $releaseRoot "Prizma-v$Version-win-x64.zip"
$buildId = [Guid]::NewGuid().ToString('N')
$stagingRoot = Join-Path $releaseRoot '.staging'
$stagingDirectory = Join-Path $stagingRoot $buildId
$stagingArchive = Join-Path $stagingRoot "$buildId.zip"
$backupDirectory = Join-Path $releaseRoot ".backup-$buildId"
$engineDirectory = Join-Path $stagingDirectory 'engine'
$winDivertDirectory = Join-Path $repositoryRoot 'artifacts\engine\windivert-windows-x64'

& (Join-Path $PSScriptRoot 'Get-WinDivert.ps1')

New-Item -ItemType Directory -Force -Path $releaseRoot, $stagingRoot | Out-Null
$resolvedRelease = [IO.Path]::GetFullPath($releaseRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
foreach ($candidate in @($publishDirectory, $stagingDirectory, $stagingArchive, $backupDirectory)) {
    $resolvedCandidate = [IO.Path]::GetFullPath($candidate)
    if (-not $resolvedCandidate.StartsWith($resolvedRelease + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Güvenli olmayan yayın yolu: $resolvedCandidate"
    }
}

try {
    & $dotnet test (Join-Path $repositoryRoot 'Prizma.sln') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Testler başarısız oldu.' }

    & $dotnet publish (Join-Path $repositoryRoot 'src\Prizma.App\Prizma.App.csproj') `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $stagingDirectory `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None
    if ($LASTEXITCODE -ne 0) { throw 'Yayın derlemesi başarısız oldu.' }

    & $dotnet publish (Join-Path $repositoryRoot 'src\Prizma.Engine\Prizma.Engine.csproj') `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $engineDirectory `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None
    if ($LASTEXITCODE -ne 0) { throw 'Birleşik motor derlemesi başarısız oldu.' }

    foreach ($fileName in @('WinDivert.dll', 'WinDivert64.sys', 'LICENSE-WinDivert.txt')) {
        $source = Join-Path $winDivertDirectory $fileName
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "WinDivert çıktısı eksik: $fileName"
        }
        Copy-Item -LiteralPath $source -Destination (Join-Path $engineDirectory $fileName)
    }

    $licenseDirectory = Join-Path $stagingDirectory 'licenses'
    New-Item -ItemType Directory -Force -Path $licenseDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination (Join-Path $licenseDirectory 'LICENSE-Prizma-MIT.txt')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'NOTICE.md') -Destination (Join-Path $licenseDirectory 'NOTICE.md')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'licenses\LICENSE-GoodbyeDPI-Apache-2.0.txt') -Destination $licenseDirectory

    foreach ($installer in @('Install-Prizma.ps1', 'Uninstall-Prizma.ps1', 'Kur.cmd', 'Kaldir.cmd')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $installer) -Destination (Join-Path $stagingDirectory $installer)
    }

    $requiredOutputs = @(
        'Prizma.exe',
        'engine\Prizma.Engine.exe',
        'engine\WinDivert.dll',
        'engine\WinDivert64.sys',
        'engine\LICENSE-WinDivert.txt',
        'licenses\LICENSE-Prizma-MIT.txt',
        'licenses\LICENSE-GoodbyeDPI-Apache-2.0.txt',
        'licenses\NOTICE.md',
        'profiles\tr\balanced.json',
        'Kur.cmd',
        'Kaldir.cmd'
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
