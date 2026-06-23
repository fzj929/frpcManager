#!/usr/bin/env bash
set -e

ROOT="$(cd "$(dirname "$0")" && pwd)"
FRONTEND_DIR="$ROOT/frontend"
BACKEND_DIR="$ROOT/backend/FrpcManager.Api"
PUBLISH_DIR="$BACKEND_DIR/publish"
SKIP_PULL=0

case "${1:-}" in
  --no-pull|/no-pull|nopull)
    SKIP_PULL=1
    ;;
esac

echo "==================================="
echo "  FrpcManager publish and start"
echo "==================================="
echo

cd "$ROOT"
if [ "$SKIP_PULL" = "1" ]; then
  echo "[1/5] Skip pulling latest code."
else
  echo "[1/5] Pull latest code..."
  git pull
fi

echo
echo "[2/5] Install frontend dependencies..."
cd "$FRONTEND_DIR"
npm install

echo
echo "[3/5] Build frontend..."
npm run build
rm -rf "$BACKEND_DIR/wwwroot"
mkdir -p "$BACKEND_DIR/wwwroot"
cp -R "$FRONTEND_DIR/dist/." "$BACKEND_DIR/wwwroot/"

echo
echo "[4/5] Publish backend..."
cd "$BACKEND_DIR"
rm -rf "$PUBLISH_DIR"
dotnet publish -c Release -o "$PUBLISH_DIR"

echo
echo "[5/5] Start published backend..."
echo "  HTTP : http://localhost:6887"
echo "  HTTPS: https://localhost:6888"
echo

cd "$PUBLISH_DIR"
export ASPNETCORE_ENVIRONMENT=Production
dotnet FrpcManager.Api.dll
