@echo off
rem Builds the Grip Zones window into dist\ (C#, the compiler that ships with Windows' .NET Framework 4), icon beside it.
rem Then `dist\GripZones.exe --selftest build\selftest` checks the zone file rules without the game, and install.ps1 puts
rem the icon on the desktop. The game half is Visceral RE8 itself (its zones script and plugin), installed with that mod.
setlocal
set HERE=%~dp0
if not exist "%HERE%dist" mkdir "%HERE%dist"
if not exist "%HERE%build" mkdir "%HERE%build"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /win32icon:"%HERE%app\grip_zones.ico" ^
   /main:Program /out:"%HERE%dist\GripZones.exe" "%HERE%app\*.cs"
if errorlevel 1 (echo APP BUILD FAILED & exit /b 1)
rem Gun Poser was built here 2026-10-28; it has its own repo since that evening (github.com/TefMeister/gun-poser-vr).
copy /y "%HERE%app\grip_zones.ico" "%HERE%dist\" >nul
echo BUILD OK: %HERE%dist
