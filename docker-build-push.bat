@echo off
setlocal

set "ROOT=%~dp0"

echo ===================================
echo   FrpcManager Docker build and push
echo ===================================
echo.

set /p DOCKER_USER=Enter Docker Hub username/namespace, not email: 
if not defined DOCKER_USER goto missing_user
if not "%DOCKER_USER:@=%"=="%DOCKER_USER%" goto invalid_user
if not "%DOCKER_USER:/=%"=="%DOCKER_USER%" goto invalid_user
if not "%DOCKER_USER:\=%"=="%DOCKER_USER%" goto invalid_user
if not "%DOCKER_USER::=%"=="%DOCKER_USER%" goto invalid_user

set /p IMAGE_REPO=Enter image repository name [frpc-manager]: 
if "%IMAGE_REPO%"=="" set "IMAGE_REPO=frpc-manager"
if not "%IMAGE_REPO:@=%"=="%IMAGE_REPO%" goto invalid_repo
if not "%IMAGE_REPO:/=%"=="%IMAGE_REPO%" goto invalid_repo
if not "%IMAGE_REPO:\=%"=="%IMAGE_REPO%" goto invalid_repo
if not "%IMAGE_REPO::=%"=="%IMAGE_REPO%" goto invalid_repo

set /p IMAGE_TAG=Enter image tag [latest]: 
if "%IMAGE_TAG%"=="" set "IMAGE_TAG=latest"

set /p NO_CACHE=Build with --no-cache? [y/N]: 
set /p DO_LOGIN=Run docker login? [Y/n]: 

set "IMAGE_NAME=%DOCKER_USER%/%IMAGE_REPO%"
set "BUILD_ARGS="
if /I "%NO_CACHE%"=="y" set "BUILD_ARGS=--no-cache"
if /I "%NO_CACHE%"=="yes" set "BUILD_ARGS=--no-cache"

echo.
echo Image: %IMAGE_NAME%:%IMAGE_TAG%
echo.

cd /d "%ROOT%"

if /I not "%DO_LOGIN%"=="n" (
    echo [1/3] Docker login...
    docker login
    if errorlevel 1 goto fail
) else (
    echo [1/3] Skip docker login.
)

echo.
echo [2/3] Build Docker image...
docker build %BUILD_ARGS% -t "%IMAGE_NAME%:%IMAGE_TAG%" .
if errorlevel 1 goto fail

echo.
echo [3/3] Push Docker image...
docker push "%IMAGE_NAME%:%IMAGE_TAG%"
if errorlevel 1 goto fail

echo.
echo Done.
echo Image pushed: %IMAGE_NAME%:%IMAGE_TAG%
echo Please confirm the Docker Hub repository visibility is Public.
goto end

:missing_user
echo.
echo Docker Hub username is required.
exit /b 1

:invalid_user
echo.
echo Invalid Docker Hub username/namespace: %DOCKER_USER%
echo Use your Docker ID, not your email address. Example: fengzhengjin929
exit /b 1

:invalid_repo
echo.
echo Invalid image repository name: %IMAGE_REPO%
echo Use a plain repository name. Example: frpc-manager
exit /b 1

:fail
echo.
echo Failed. Please check the error above.
pause
exit /b 1

:end
endlocal
