@echo off
setlocal EnableExtensions
rem ClickCircle uninstaller: remove the three plugin files, keep settings and logs.
rem Usage: uninstall.cmd                  (this folder sits inside the PDF-XChange Editor folder)
rem        uninstall.cmd "editor folder"  (explicit editor folder)

set "SELF=%~dp0"
if "%SELF:~-1%"=="\" set "SELF=%SELF:~0,-1%"
set "EDITOR=%~1"
if not defined EDITOR set "EDITOR=%SELF%\.."
for %%I in ("%EDITOR%") do set "EDITOR=%%~fI"
set "PLUGINS=%EDITOR%\Plugins.x64"

echo ClickCircle uninstaller
echo   editor : %EDITOR%
echo.

set "EXENAME="
if exist "%EDITOR%\PDFXEdit.exe" set "EXENAME=PDFXEdit.exe"
if not defined EXENAME if exist "%EDITOR%\PXCEditor.exe" set "EXENAME=PXCEditor.exe"
if not defined EXENAME goto :no_editor

tasklist /fi "imagename eq %EXENAME%" 2>nul | find /i "%EXENAME%" >nul
if not errorlevel 1 goto :running

if not exist "%PLUGINS%\ClickCircle.pvp" if not exist "%PLUGINS%\ClickCircle.Core.dll" if not exist "%PLUGINS%\PDFXEdit.dll" goto :not_installed

if not exist "%SELF%\output" mkdir "%SELF%\output" || goto :dir_failed
set "STAMP="
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"`) do set "STAMP=%%I"
if not defined STAMP set "STAMP=%RANDOM%%RANDOM%"
set "BACKUP=%SELF%\output\uninstall-backup-%STAMP%"
mkdir "%BACKUP%" || goto :dir_failed
for %%F in (ClickCircle.pvp ClickCircle.Core.dll PDFXEdit.dll ClickCircle.install.json) do (
    if exist "%PLUGINS%\%%F" copy /y "%PLUGINS%\%%F" "%BACKUP%\%%F" >nul
)

for %%F in (ClickCircle.pvp ClickCircle.Core.dll PDFXEdit.dll ClickCircle.install.json) do (
    if exist "%PLUGINS%\%%F" del /q "%PLUGINS%\%%F" || goto :delete_failed
)

echo Removed the three plugin files. Previous copy:
echo   %BACKUP%
echo Settings, logs and subject history are kept in Plugins.x64\ClickCircleData.
echo.
pause
exit /b 0

:no_editor
echo ERROR: PDF-XChange Editor was not found in
echo   %EDITOR%
echo Put this script inside the editor's CircleStampPlugin folder, or pass the
echo editor folder as an argument:
echo   uninstall.cmd "D:\...\PDF Editor"
pause
exit /b 1

:running
echo ERROR: %EXENAME% is running. Save your work, close the editor, then run again.
pause
exit /b 1

:not_installed
echo Nothing to remove: ClickCircle files were not found in
echo   %PLUGINS%
pause
exit /b 0

:delete_failed
echo ERROR: delete failed. Make sure the editor is fully closed and check permissions.
pause
exit /b 1

:dir_failed
echo ERROR: cannot create the backup folder. Check folder permissions.
pause
exit /b 1
