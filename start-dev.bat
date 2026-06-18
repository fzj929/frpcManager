@echo off
echo ===================================
echo   FrpC 管理平台 - 启动脚本
echo ===================================

echo.
echo [1/2] 启动后端服务 (HTTP 6887 / HTTPS 6888)...
start "FrpcManager Backend" cmd /k "cd backend\FrpcManager.Api && dotnet run"

echo.
echo [2/2] 等待 3 秒后启动前端开发服务器...
timeout /t 3 /nobreak >nul

start "FrpcManager Frontend" cmd /k "cd frontend && npm run dev"

echo.
echo 服务已启动！
echo   后端 API：http://localhost:6887
echo   后端 HTTPS：https://localhost:6888
echo   前端页面：http://localhost:5173
echo   Swagger：https://localhost:6888/swagger
echo.
echo 默认账号：admin / admin123
pause
