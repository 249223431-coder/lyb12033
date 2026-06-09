@echo off
chcp 65001 >nul
title 班组管理系统

echo ========================================
echo         班组管理系统 - 启动中...
echo ========================================
echo.

cd /d "%~dp0"

where python >nul 2>nul
if %errorlevel% neq 0 (
    echo [错误] 未检测到Python，请先安装Python 3.9或以上版本
    echo 下载地址: https://www.python.org/downloads/
    echo 安装时请勾选 "Add Python to PATH"
    pause
    exit /b 1
)

python --version

if not exist "venv\" (
    echo.
    echo [1/3] 正在创建虚拟环境...
    python -m venv venv
    if %errorlevel% neq 0 (
        echo [错误] 创建虚拟环境失败
        pause
        exit /b 1
    )
)

echo.
echo [2/3] 正在安装依赖包...
call venv\Scripts\activate.bat
pip install -r requirements.txt -i https://pypi.tuna.tsinghua.edu.cn/simple
if %errorlevel% neq 0 (
    echo [警告] 国内镜像安装失败，尝试官方源...
    pip install -r requirements.txt
)

echo.
echo [3/3] 正在启动服务...
echo.
echo ========================================
echo   服务已启动！(Waitress 生产模式)
echo   手机访问地址: http://你的IP地址:3000
echo   本机访问地址: http://localhost:3000
echo   管理员账号: admin / admin123
echo   按 Ctrl+C 可停止服务
echo ========================================
echo.

:: 默认使用 Waitress 生产模式启动 (16线程 + 连接优化)
python -c "from waitress import serve; from app import create_app; serve(create_app(), host='0.0.0.0', port=3000, threads=16, connection_limit=100, channel_timeout=120, max_request_body_size=536870912)"
:: 如需调试模式，使用: python app.py --dev
pause
