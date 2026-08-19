[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sourceDirectory = [IO.Path]::GetFullPath($PSScriptRoot)
$installDirectory = Join-Path $env:ProgramFiles 'Prizma'
if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory 'Prizma.exe') -PathType Leaf)) {
    throw 'Kurulum betiği yayın paketinin içinden çalıştırılmalı.'
}

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments -Wait
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null
$excluded = @('Install-Prizma.ps1', 'Uninstall-Prizma.ps1', 'Kur.cmd', 'Kaldir.cmd')
Get-ChildItem -LiteralPath $sourceDirectory | Where-Object { $_.Name -notin $excluded } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $installDirectory -Recurse -Force
}

$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path ([Environment]::GetFolderPath('CommonStartMenu')) 'Programs\Prizma.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDirectory 'Prizma.exe'
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Description = 'Prizma bağlantı koruması'
$shortcut.Save()

Start-Process -FilePath (Join-Path $installDirectory 'Prizma.exe')
