#!/usr/bin/env bash
set -e

ROOT="$(cd "$(dirname "$0")" && pwd)"
FRONTEND_DIR="$ROOT/frontend"
BACKEND_DIR="$ROOT/backend/FrpcManager.Api"
PUBLISH_DIR="$BACKEND_DIR/publish"

echo "==================================="
echo "  FrpcManager publish and start"
echo "==================================="
echo

cd "$ROOT"
echo "[1/5] Pull latest code..."
git pull

echo
echo "[2/5] Install frontend dependencies..."
cd "$FRONTEND_DIR"
npm install

echo
echo "[3/5] Build frontend..."
npm run build

echo
echo "[4/5] Publish backend..."
cd "$BACKEND_DIR"
dotnet publish -c Release -o "$PUBLISH_DIR"

echo
echo "[5/5] Start published backend..."
echo "  HTTP : http://localhost:6665"
echo "  HTTPS: https://localhost:6666"
echo

cd "$PUBLISH_DIR"
export ASPNETCORE_ENVIRONMENT=Production
dotnet FrpcManager.Api.dll
