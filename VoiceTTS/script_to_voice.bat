@echo off
chcp 65001 >nul
set PYTHONIOENCODING=utf-8
cd /d "%~dp0"
if "%~1"=="" (
  echo Drag a UTF-8 .txt script onto this file. One line = one audio file.
  pause & exit /b
)
.venv\Scripts\python.exe tts.py --file "%~1"
pause
