[CmdletBinding()]
param(
    [string]$Version = 'v1.0.4'
)

$ErrorActionPreference = 'Stop'
if ($Version -ne 'v1.0.4') {
    throw "Bu betik yalnızca özeti sabitlenmiş v1.0.4 sürümünü kabul eder. İstenen: $Version"
}

$expectedSha256 = '5760b6d41c09459fff00b4a6fec5437a471a00aac15f734723ede149cd26c709'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$engineTarget = Join-Path $artifactsRoot 'engine\windows-x64'
$downloadDirectory = Join-Path $artifactsRoot 'downloads'
$archivePath = Join-Path $downloadDirectory "zapret2-$Version.zip"
$downloadUrl = "https://github.com/bol-van/zapret2/releases/download/$Version/zapret2-$Version.zip"

New-Item -ItemType Directory -Force -Path $downloadDirectory, $engineTarget | Out-Null

if (-not (Test-Path -LiteralPath $archivePath)) {
    Write-Host "zapret2 $Version indiriliyor..."
    Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $archivePath
}

$actualSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
if ($actualSha256 -ne $expectedSha256) {
    throw "zapret2 arşiv özeti uyuşmuyor. Beklenen: $expectedSha256, alınan: $actualSha256"
}

$temporaryRoot = Join-Path $artifactsRoot ("extract-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null

try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $temporaryRoot
    $bundleRoot = Get-ChildItem -LiteralPath $temporaryRoot -Directory | Select-Object -First 1
    if ($null -eq $bundleRoot) {
        throw 'zapret2 arşiv kökü bulunamadı.'
    }

    $windowsEngine = Join-Path $bundleRoot.FullName 'binaries\windows-x86_64'
    $licensePath = Join-Path $bundleRoot.FullName 'docs\LICENSE.txt'
    $requiredFiles = @('winws2.exe', 'cygwin1.dll', 'WinDivert.dll', 'WinDivert64.sys')

    foreach ($fileName in $requiredFiles) {
        $source = Join-Path $windowsEngine $fileName
        if (-not (Test-Path -LiteralPath $source)) {
            throw "Arşivde gerekli motor dosyası yok: $fileName"
        }
        Copy-Item -Force -LiteralPath $source -Destination (Join-Path $engineTarget $fileName)
    }

    if (Test-Path -LiteralPath $licensePath) {
        Copy-Item -Force -LiteralPath $licensePath -Destination (Join-Path $engineTarget 'THIRD-PARTY-LICENSE-zapret2.txt')
    }
}
finally {
    $resolvedArtifacts = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolvedTemporary = [IO.Path]::GetFullPath($temporaryRoot)
    if ($resolvedTemporary.StartsWith($resolvedArtifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -Recurse -Force -LiteralPath $resolvedTemporary
    }
}

Write-Host "Motor hazır: $engineTarget"
