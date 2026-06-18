@echo off
setlocal

set "ROOT=%~dp0"
set "FRONTEND_DIR=%ROOT%frontend"
set "BACKEND_DIR=%ROOT%backend\FrpcManager.Api"
set "PUBLISH_DIR=%BACKEND_DIR%\publish"

echo ===================================
echo   FrpcManager publish and start
echo ===================================
echo.

cd /d "%ROOT%"
echo [1/5] Pull latest code...
git pull
if errorlevel 1 goto fail

echo.
echo [2/5] Install frontend dependencies...
cd /d "%FRONTEND_DIR%"
npm install
if errorlevel 1 goto fail

echo.
echo [3/5] Build frontend...
npm run build
if errorlevel 1 goto fail

echo.
echo [4/5] Publish backend...
cd /d "%BACKEND_DIR%"
dotnet publish -c Release -o "%PUBLISH_DIR%"
if errorlevel 1 goto fail

echo.
echo [5/5] Start published backend...
echo   HTTP : http://localhost:6665
echo   HTTPS: https://localhost:6666
echo.
cd /d "%PUBLISH_DIR%"
set "ASPNETCORE_ENVIRONMENT=Production"
dotnet FrpcManager.Api.dll
goto end

:fail
echo.
echo Failed. Please check the error above.
pause
exit /b 1

:end
endlocal
