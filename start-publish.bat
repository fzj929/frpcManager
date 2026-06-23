@echo off
setlocal

set "ROOT=%~dp0"
set "FRONTEND_DIR=%ROOT%frontend"
set "BACKEND_DIR=%ROOT%backend\FrpcManager.Api"
set "PUBLISH_DIR=%BACKEND_DIR%\publish"
set "SKIP_PULL=0"

if /I "%~1"=="--no-pull" set "SKIP_PULL=1"
if /I "%~1"=="/no-pull" set "SKIP_PULL=1"
if /I "%~1"=="nopull" set "SKIP_PULL=1"

echo ===================================
echo   FrpcManager publish and start
echo ===================================
echo.

cd /d "%ROOT%"
if "%SKIP_PULL%"=="1" (
    echo [1/5] Skip pulling latest code.
) else (
    echo [1/5] Pull latest code...
    git pull
    if errorlevel 1 goto fail
)

echo.
echo [2/5] Install frontend dependencies...
cd /d "%FRONTEND_DIR%"
call npm install
if errorlevel 1 goto fail

echo.
echo [3/5] Build frontend...
call npm run build
if errorlevel 1 goto fail
if exist "%BACKEND_DIR%\wwwroot" rmdir /s /q "%BACKEND_DIR%\wwwroot"
xcopy "%FRONTEND_DIR%\dist" "%BACKEND_DIR%\wwwroot" /E /I /Y >nul
if errorlevel 1 goto fail

echo.
echo [4/5] Publish backend...
cd /d "%BACKEND_DIR%"
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
dotnet publish -c Release -o "%PUBLISH_DIR%"
if errorlevel 1 goto fail

echo.
echo [5/5] Start published backend...
echo   HTTP : http://localhost:6887
echo   HTTPS: https://localhost:6888
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
