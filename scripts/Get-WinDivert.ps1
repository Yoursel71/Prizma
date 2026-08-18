[CmdletBinding()]
param(
    [string]$Version = '2.2.2'
)

$ErrorActionPreference = 'Stop'
if ($Version -ne '2.2.2') {
    throw "Bu betik yalnızca özeti sabitlenmiş WinDivert 2.2.2 sürümünü kabul eder. İstenen: $Version"
}

$expectedSha256 = '63cb41763bb4b20f600b6de04e991a9c2be73279e317d4d82f237b150c5f3f15'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$downloadDirectory = Join-Path $artifactsRoot 'downloads'
$engineRoot = Join-Path $artifactsRoot 'engine'
$engineTarget = Join-Path $engineRoot 'windivert-windows-x64'
$archivePath = Join-Path $downloadDirectory "WinDivert-$Version-A.zip"
$downloadUrl = "https://github.com/basil00/WinDivert/releases/download/v$Version/WinDivert-$Version-A.zip"
$operationId = [Guid]::NewGuid().ToString('N')
$temporaryRoot = Join-Path $artifactsRoot "extract-windivert-$operationId"
$stagingTarget = Join-Path $engineRoot ".windivert-stage-$operationId"
$backupTarget = Join-Path $engineRoot ".windivert-backup-$operationId"

New-Item -ItemType Directory -Force -Path $downloadDirectory, $engineRoot | Out-Null
if (-not (Test-Path -LiteralPath $archivePath)) {
    Write-Host "WinDivert $Version indiriliyor..."
    Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $archivePath
}

$actualSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
if ($actualSha256 -ne $expectedSha256) {
    throw "WinDivert arşiv özeti uyuşmuyor. Beklenen: $expectedSha256, alınan: $actualSha256"
}

$resolvedArtifacts = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
foreach ($candidate in @($temporaryRoot, $stagingTarget, $backupTarget, $engineTarget)) {
    $resolvedCandidate = [IO.Path]::GetFullPath($candidate)
    if (-not $resolvedCandidate.StartsWith($resolvedArtifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Güvenli olmayan motor yolu: $resolvedCandidate"
    }
}

New-Item -ItemType Directory -Path $temporaryRoot, $stagingTarget | Out-Null
try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $temporaryRoot
    $archiveRoot = Join-Path $temporaryRoot "WinDivert-$Version-A"
    $x64Root = Join-Path $archiveRoot 'x64'
    foreach ($fileName in @('WinDivert.dll', 'WinDivert64.sys')) {
        $source = Join-Path $x64Root $fileName
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Arşivde gerekli WinDivert dosyası yok: $fileName"
        }
        Copy-Item -LiteralPath $source -Destination (Join-Path $stagingTarget $fileName)
    }

    $licenseSource = Join-Path $archiveRoot 'LICENSE'
    if (-not (Test-Path -LiteralPath $licenseSource -PathType Leaf)) {
        throw 'Arşivde WinDivert LICENSE dosyası yok.'
    }
    Copy-Item -LiteralPath $licenseSource -Destination (Join-Path $stagingTarget 'LICENSE-WinDivert.txt')

    if (Test-Path -LiteralPath $engineTarget) {
        Move-Item -LiteralPath $engineTarget -Destination $backupTarget
    }
    try {
        Move-Item -LiteralPath $stagingTarget -Destination $engineTarget
    }
    catch {
        if (Test-Path -LiteralPath $backupTarget) {
            Move-Item -LiteralPath $backupTarget -Destination $engineTarget
        }
        throw
    }

    if (Test-Path -LiteralPath $backupTarget) {
        Remove-Item -Recurse -Force -LiteralPath $backupTarget
    }
}
finally {
    foreach ($candidate in @($temporaryRoot, $stagingTarget)) {
        if (Test-Path -LiteralPath $candidate) {
            Remove-Item -Recurse -Force -LiteralPath $candidate
        }
    }
}

Write-Host "WinDivert hazır: $engineTarget"
