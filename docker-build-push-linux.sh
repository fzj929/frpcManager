#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
export DOCKER_BUILDKIT=1

echo "==================================="
echo "  FrpcManager Docker build and push"
echo "==================================="
echo

read -r -p "Enter Docker Hub username/namespace, not email: " DOCKER_USER
if [ -z "$DOCKER_USER" ]; then
  echo "Docker Hub username is required."
  exit 1
fi
if [[ "$DOCKER_USER" == *"@"* || "$DOCKER_USER" == *"/"* || "$DOCKER_USER" == *"\\"* || "$DOCKER_USER" == *":"* ]]; then
  echo "Invalid Docker Hub username/namespace: $DOCKER_USER"
  echo "Use your Docker ID, not your email address. Example: fengzhengjin929"
  exit 1
fi

read -r -p "Enter image repository name [frpc-manager]: " IMAGE_REPO
IMAGE_REPO="${IMAGE_REPO:-frpc-manager}"
if [[ "$IMAGE_REPO" == *"@"* || "$IMAGE_REPO" == *"/"* || "$IMAGE_REPO" == *"\\"* || "$IMAGE_REPO" == *":"* ]]; then
  echo "Invalid image repository name: $IMAGE_REPO"
  echo "Use a plain repository name. Example: frpc-manager"
  exit 1
fi

read -r -p "Enter image tag [latest]: " IMAGE_TAG
IMAGE_TAG="${IMAGE_TAG:-latest}"

read -r -p "Build with --no-cache? [y/N]: " NO_CACHE
read -r -p "Run docker login? [Y/n]: " DO_LOGIN

IMAGE_NAME="$DOCKER_USER/$IMAGE_REPO"
BUILD_ARGS=()
case "${NO_CACHE,,}" in
  y|yes)
    BUILD_ARGS+=(--no-cache)
    ;;
esac

echo
echo "Image: $IMAGE_NAME:$IMAGE_TAG"
echo

cd "$ROOT"

case "${DO_LOGIN,,}" in
  n|no)
    echo "[1/3] Skip docker login."
    ;;
  *)
    echo "[1/3] Docker login..."
    docker login
    ;;
esac

echo
echo "[2/3] Build Docker image..."
docker build "${BUILD_ARGS[@]}" -t "$IMAGE_NAME:$IMAGE_TAG" .

echo
echo "[3/3] Push Docker image..."
docker push "$IMAGE_NAME:$IMAGE_TAG"

echo
echo "Done."
echo "Image pushed: $IMAGE_NAME:$IMAGE_TAG"
echo "Please confirm the Docker Hub repository visibility is Public."
