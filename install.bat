@echo off
cls
title 班组管理系统 - 一键安装
color 0A

echo ========================================
echo    班组管理系统 - 一键安装脚本
echo ========================================
echo.

REM 检查Python是否安装
echo [1/6] 检查Python环境...
python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python未安装！请先安装Python 3.8+
    echo 下载地址: https://www.python.org/downloads/
    pause
    exit /b 1
)
echo    Python版本:
python --version

echo.
echo [2/6] 创建虚拟环境...
if not exist "venv" (
    python -m venv venv
    echo    虚拟环境创建成功
) else (
    echo    虚拟环境已存在
)

echo.
echo [3/6] 激活虚拟环境并安装依赖...
call venv\Scripts\activate.bat
pip install -r requirements.txt

echo.
echo [4/6] 初始化数据库...
python -c "from app import create_app; app = create_app(); app.app_context().push(); from models import db; db.create_all(); print('数据库初始化成功')"

echo.
echo [5/6] 创建必要的目录...
mkdir static\uploads\BOM_清单 2>nul
mkdir static\uploads\MCS图纸 2>nul
mkdir backups 2>nul

echo.
echo [6/6] 创建桌面快捷方式...
echo @echo off > "%USERPROFILE%\Desktop\启动班组系统.bat"
echo cd /d "%cd%" >> "%USERPROFILE%\Desktop\启动班组系统.bat"
echo call venv\Scripts\activate.bat >> "%USERPROFILE%\Desktop\启动班组系统.bat"
echo python start_server.py >> "%USERPROFILE%\Desktop\启动班组系统.bat" >> "%USERPROFILE%\Desktop\启动班组系统.bat"

echo.
echo ========================================
echo    安装完成！
echo ========================================
echo.
echo 使用说明：
echo 1. 双击桌面上的"启动班组系统.bat"启动服务
echo 2. 打开浏览器访问 http://localhost:3000
echo 3. 默认管理员账号: admin / 123456
echo.
echo 注意：首次启动需要加载BOM缓存，可能需要几分钟
echo.
pause