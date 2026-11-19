@echo off
setlocal EnableExtensions
rem ClickCircle installer: copy the three dist files into the editor's Plugins.x64.
rem Usage: install.cmd                  (this folder sits inside the PDF-XChange Editor folder)
rem        install.cmd "editor folder"  (explicit editor folder)

set "SELF=%~dp0"
if "%SELF:~-1%"=="\" set "SELF=%SELF:~0,-1%"
set "EDITOR=%~1"
if not defined EDITOR set "EDITOR=%SELF%\.."
for %%I in ("%EDITOR%") do set "EDITOR=%%~fI"
set "DIST=%SELF%\dist"
set "PLUGINS=%EDITOR%\Plugins.x64"

echo ClickCircle installer
echo   editor : %EDITOR%
echo.

if not exist "%DIST%\ClickCircle.pvp" goto :no_dist
if not exist "%DIST%\ClickCircle.Core.dll" goto :no_dist
if not exist "%DIST%\PDFXEdit.dll" goto :no_dist

set "EXENAME="
if exist "%EDITOR%\PDFXEdit.exe" set "EXENAME=PDFXEdit.exe"
if not defined EXENAME if exist "%EDITOR%\PXCEditor.exe" set "EXENAME=PXCEditor.exe"
if not defined EXENAME goto :no_editor

tasklist /fi "imagename eq %EXENAME%" 2>nul | find /i "%EXENAME%" >nul
if not errorlevel 1 goto :running

if not exist "%PLUGINS%" mkdir "%PLUGINS%" || goto :dir_failed
if not exist "%SELF%\output" mkdir "%SELF%\output" || goto :dir_failed
set "STAMP="
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"`) do set "STAMP=%%I"
if not defined STAMP set "STAMP=%RANDOM%%RANDOM%"
set "BACKUP=%SELF%\output\install-backup-%STAMP%"
mkdir "%BACKUP%" || goto :dir_failed

for %%F in (ClickCircle.pvp ClickCircle.Core.dll PDFXEdit.dll) do (
    if exist "%PLUGINS%\%%F" copy /y "%PLUGINS%\%%F" "%BACKUP%\%%F" >nul
)
if exist "%PLUGINS%\ClickCircle.install.json" copy /y "%PLUGINS%\ClickCircle.install.json" "%BACKUP%\ClickCircle.install.json" >nul

for %%F in (ClickCircle.pvp ClickCircle.Core.dll PDFXEdit.dll) do (
    copy /y "%DIST%\%%F" "%PLUGINS%\%%F" >nul || goto :copy_failed
    fc /b "%DIST%\%%F" "%PLUGINS%\%%F" >nul || goto :copy_failed
)
if exist "%PLUGINS%\ClickCircle.install.json" del /q "%PLUGINS%\ClickCircle.install.json"

echo Installed 3 files to:
echo   %PLUGINS%
echo Backup of the previous version:
echo   %BACKUP%
echo Restart PDF-XChange Editor. The panel title shows the version.
echo.
pause
exit /b 0

:no_editor
echo ERROR: PDF-XChange Editor was not found in
echo   %EDITOR%
echo Put this script inside the editor's CircleStampPlugin folder, or pass the
echo editor folder as an argument:
echo   install.cmd "D:\...\PDF Editor"
pause
exit /b 1

:no_dist
echo ERROR: release files are missing in
echo   %DIST%
echo Expected ClickCircle.pvp, ClickCircle.Core.dll and PDFXEdit.dll.
pause
exit /b 1

:running
echo ERROR: %EXENAME% is running. Save your work, close the editor, then run again.
pause
exit /b 1

:copy_failed
echo ERROR: copy or verification failed. Check file locks, disk space and permissions.
pause
exit /b 1

:dir_failed
echo ERROR: cannot create the plugin or backup folder. Check folder permissions.
pause
exit /b 1
