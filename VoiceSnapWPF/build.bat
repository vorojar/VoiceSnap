@echo off
chcp 65001 >nul
echo ========================================
echo   VoiceSnap 语闪 - 完整打包发布
echo   创建独立运行的发布包
echo ========================================
echo.

cd /d "%~dp0"

set PUBLISH_DIR=.\publish
set FUN_ASR_ROOT=..

echo [1/5] 清理旧的发布目录...
if exist "%PUBLISH_DIR%" rd /s /q "%PUBLISH_DIR%"
mkdir "%PUBLISH_DIR%"

echo [2/5] 编译 C# 程序 (Self-Contained)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "%PUBLISH_DIR%"
if errorlevel 1 goto :error

echo [3/5] 复制 Python 脚本...
copy /Y "PythonBackend\asr_service.py" "%PUBLISH_DIR%\" >nul

echo [4/5] 复制模型和代码文件...
:: 复制 model.py
copy /Y "%FUN_ASR_ROOT%\model.py" "%PUBLISH_DIR%\" >nul

:: 图标已作为资源嵌入 VoiceSnap.exe，无需复制 Assets 文件夹

:: 复制模型目录
echo 正在复制模型目录 (这可能需要一些时间)...
if not exist "%PUBLISH_DIR%\models" mkdir "%PUBLISH_DIR%\models"
xcopy /E /I /Q /Y "%FUN_ASR_ROOT%\models\Fun-ASR-Nano-2512" "%PUBLISH_DIR%\models\Fun-ASR-Nano-2512" >nul

echo [5/5] 清理不必要的文件...
:: 删除调试符号
if exist "%PUBLISH_DIR%\VoiceSnap.pdb" del /F /Q "%PUBLISH_DIR%\VoiceSnap.pdb"

:: 删除模型目录中的文档和示例
set MODEL_PATH=%PUBLISH_DIR%\models\Fun-ASR-Nano-2512
if exist "%MODEL_PATH%" (
    if exist "%MODEL_PATH%\README.md" del /F /Q "%MODEL_PATH%\README.md"
    if exist "%MODEL_PATH%\README_zh.md" del /F /Q "%MODEL_PATH%\README_zh.md"
    if exist "%MODEL_PATH%\example" rd /s /q "%MODEL_PATH%\example"
    if exist "%MODEL_PATH%\images" rd /s /q "%MODEL_PATH%\images"
)

:: 删除所有 __pycache__
powershell -Command "Get-ChildItem -Path '%PUBLISH_DIR%' -Filter '__pycache__' -Recurse | Remove-Item -Force -Recurse"

echo [6/6] 创建说明文件...
(
echo ========================================
echo   VoiceSnap 语闪 - 使用说明
echo ========================================
echo.
echo 双击 VoiceSnap.exe 即可启动程序
echo.
echo 【使用方法】
echo 1. 启动后等待模型加载完成
echo 2. 在任意输入框中长按 Ctrl 键
echo 3. 对着麦克风说话
echo 4. 松开 Ctrl 键，文字自动输入
echo.
echo 【系统要求】
echo - Windows 10/11 64位
echo - Python 3.9+ (已安装到 PATH^)
echo - 需要安装: pip install fastapi uvicorn funasr sounddevice soundfile torch
echo.
echo 【文件说明】
echo - VoiceSnap.exe : 主程序
echo - asr_service.py : ASR 服务
echo - model.py : 模型代码
echo - models/ : 模型文件
echo.
) > "%PUBLISH_DIR%\使用说明.txt"

echo.
echo ========================================
echo   ✓ 打包完成！
echo ========================================
echo.
echo 发布目录: %CD%\publish\
echo.

:: 计算目录大小
for /f "tokens=3" %%a in ('dir "%PUBLISH_DIR%" /s /-c ^| findstr "个文件"') do set SIZE=%%a
echo 总大小: 约 %SIZE:~0,-6% MB

echo.
echo 用户只需要:
echo 1. 安装 Python 3.9+ (确保在 PATH 中)
echo 2. pip install fastapi uvicorn funasr sounddevice soundfile torch
echo 3. 双击 VoiceSnap.exe
echo.
pause
exit /b 0

:error
echo.
echo ========================================
echo   ✗ 打包失败！
echo ========================================
echo.
pause
exit /b 1
