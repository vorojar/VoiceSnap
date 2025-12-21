@echo off
chcp 65001 >nul
echo ========================================
echo   VoiceSnap 语闪 - C# WPF 版本
echo   极速启动 · 丝滑动画
echo ========================================
echo.

cd /d "%~dp0"

echo [1/2] 启动 Python 后端服务...
start /B python PythonBackend\asr_service.py

echo [2/2] 等待后端启动 (2秒)...
timeout /t 2 /nobreak >nul

echo [3/3] 启动 C# WPF 客户端...
dotnet run --project VoiceSnap.csproj

pause
