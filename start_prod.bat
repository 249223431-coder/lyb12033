@echo off
chcp 65001 >nul
title 班组管理系统 - 生产模式

echo ========================================
echo    班组管理系统 - 生产模式启动
echo ========================================

cd /d "%~dp0"

call venv\Scripts\activate.bat

echo 使用waitress启动 (适合生产环境)...
python -c "from waitress import serve; from app import create_app; serve(create_app(), host='0.0.0.0', port=3000, threads=16, connection_limit=100, channel_timeout=120, max_request_body_size=536870912)"
pause
