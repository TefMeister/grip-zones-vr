# install.ps1 -- copies Grip Zones from dist\ to %LOCALAPPDATA%\Programs\GripZones and puts its icon on the desktop.
#   powershell -ExecutionPolicy Bypass -File install.ps1 [-From <folder with the built files>]
param([string]$From = "")
if ($From -eq "") { $From = if (Test-Path (Join-Path $PSScriptRoot "dist\GripZones.exe")) { Join-Path $PSScriptRoot "dist" } else { $PSScriptRoot } }
$ErrorActionPreference = "Stop"
$to = Join-Path $env:LOCALAPPDATA "Programs\GripZones"
New-Item -ItemType Directory -Force $to | Out-Null
foreach ($f in "GripZones.exe", "GunPoser.exe", "grip_zones.ico") {
    if (Test-Path (Join-Path $From $f)) { Copy-Item (Join-Path $From $f) (Join-Path $to $f) -Force }
}
$desktop = [Environment]::GetFolderPath("Desktop")
# the app was called Grab Zones until 2026-10-27: its old desktop icon goes, so only one is left
$old = Join-Path $desktop "Grab Zones.lnk"
if (Test-Path $old) { Remove-Item $old -Force }
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut((Join-Path $desktop "Grip Zones.lnk"))
$lnk.TargetPath = Join-Path $to "GripZones.exe"
$lnk.WorkingDirectory = $to
$lnk.IconLocation = (Join-Path $to "grip_zones.ico") + ",0"
$lnk.Description = "Move and resize where the hand can grip a gun in VR"
$lnk.Save()
# 2026-10-28: Dock Poser moved into Hand Poser (its LEFT HAND, DOCKED box): its program and desktop icon go
foreach ($gone in (Join-Path $to "DockPoser.exe"), (Join-Path $desktop "Dock Poser.lnk")) { if (Test-Path $gone) { Remove-Item $gone -Force } }
# 2026-10-28: Gun Poser, the gun in the right hand per gun
if (Test-Path (Join-Path $to "GunPoser.exe")) {
    $d = $shell.CreateShortcut((Join-Path $desktop "Gun Poser.lnk"))
    $d.TargetPath = Join-Path $to "GunPoser.exe"
    $d.WorkingDirectory = $to
    $d.IconLocation = (Join-Path $to "grip_zones.ico") + ",0"
    $d.Description = "Aim and move the gun in the right hand in VR, per gun"
    $d.Save()
}
Write-Output "Installed to $to, icon on the desktop."
