[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sourceDirectory = [IO.Path]::GetFullPath($PSScriptRoot)
$installDirectory = Join-Path $env:ProgramFiles 'ZapretTR'
if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory 'ZapretTR.exe') -PathType Leaf)) {
    throw 'Kurulum betiği yayın paketinin içinden çalıştırılmalı.'
}

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments -Wait
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null
$excluded = @('Install-ZapretTR.ps1', 'Uninstall-ZapretTR.ps1', 'Kur.cmd', 'Kaldir.cmd')
Get-ChildItem -LiteralPath $sourceDirectory | Where-Object { $_.Name -notin $excluded } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $installDirectory -Recurse -Force
}

$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path ([Environment]::GetFolderPath('CommonStartMenu')) 'Programs\ZapretTR.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDirectory 'ZapretTR.exe'
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Description = 'ZapretTR bağlantı koruması'
$shortcut.Save()

Start-Process -FilePath (Join-Path $installDirectory 'ZapretTR.exe')
