@echo off
chcp 65001 >nul
set PYTHONIOENCODING=utf-8
cd /d "%~dp0"
start "" http://127.0.0.1:7860
.venv\Scripts\omnivoice-demo.exe --ip 127.0.0.1 --port 7860
