[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments -Wait
    exit $LASTEXITCODE
}

$service = Get-Service -Name 'ZapretTR.Engine' -ErrorAction SilentlyContinue
if ($null -ne $service) {
    if ($service.Status -ne 'Stopped') { Stop-Service -Name 'ZapretTR.Engine' -Force }
    & "$env:SystemRoot\System32\sc.exe" delete 'ZapretTR.Engine' | Out-Null
}

$shortcutPath = Join-Path ([Environment]::GetFolderPath('CommonStartMenu')) 'Programs\ZapretTR.lnk'
if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath -Force }

$installDirectory = Join-Path $env:ProgramFiles 'ZapretTR'
if (Test-Path -LiteralPath $installDirectory) { Remove-Item -LiteralPath $installDirectory -Recurse -Force }
