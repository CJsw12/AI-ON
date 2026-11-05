@echo off
chcp 65001 >nul
set PYTHONIOENCODING=utf-8
cd /d "%~dp0"
echo [My voice TTS] best model: CosyVoice3  (all models: speak_all.bat)
:loop
set "T="
set /p T=Text (empty = quit): 
if "%T%"=="" exit /b
.venv\Scripts\python.exe tts.py "%T%"
goto loop
