@echo off
chcp 65001 >nul
set PYTHONIOENCODING=utf-8
cd /d "%~dp0"
if "%~1"=="" (
  echo Drag your recording file onto this .bat file.
  pause & exit /b
)
.venv\Scripts\python.exe compare.py "%~1"
start "" output
pause
