#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"

DEFAULT_REPOSITORY="frpc-manager"
DEFAULT_DESCRIPTION="FrpC web management platform for frpc tunnels, with setup wizard, audit logs, health checks, backup/restore, SQLite and MySQL support."
README_PATH="$ROOT/dockerhub-description.md"

read -r -p "Enter Docker Hub username: " USERNAME
read -r -p "Enter Docker Hub namespace/repository owner: " NAMESPACE
read -r -p "Enter Docker Hub repository name [$DEFAULT_REPOSITORY]: " REPOSITORY
REPOSITORY="${REPOSITORY:-$DEFAULT_REPOSITORY}"
read -r -p "Enter short description [$DEFAULT_DESCRIPTION]: " DESCRIPTION
DESCRIPTION="${DESCRIPTION:-$DEFAULT_DESCRIPTION}"
read -r -s -p "Enter Docker Hub password or access token: " PASSWORD_OR_TOKEN
echo

if [[ -z "$USERNAME" ]]; then
  echo "Docker Hub username is required."
  exit 1
fi

if [[ "$NAMESPACE" == *"@"* || "$NAMESPACE" == *"/"* || "$NAMESPACE" == *"\\"* || "$NAMESPACE" == *":"* || -z "$NAMESPACE" ]]; then
  echo "Invalid namespace '$NAMESPACE'. Use Docker Hub ID/namespace, not an email address."
  exit 1
fi

if [[ "$REPOSITORY" == *"@"* || "$REPOSITORY" == *"/"* || "$REPOSITORY" == *"\\"* || "$REPOSITORY" == *":"* || -z "$REPOSITORY" ]]; then
  echo "Invalid repository '$REPOSITORY'. Use only the repository name, for example frpc-manager."
  exit 1
fi

if [[ ! -f "$README_PATH" ]]; then
  echo "Docker Hub README file not found: $README_PATH"
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required."
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required."
  exit 1
fi

echo "Logging in to Docker Hub API..."
JWT="$(
  jq -n --arg username "$USERNAME" --arg password "$PASSWORD_OR_TOKEN" \
    '{username: $username, password: $password}' |
  curl -fsS \
    -H "Content-Type: application/json" \
    -X POST \
    -d @- \
    "https://hub.docker.com/v2/users/login/" |
  jq -r '.token'
)"

if [[ -z "$JWT" || "$JWT" == "null" ]]; then
  echo "Docker Hub API did not return a token."
  exit 1
fi

echo "Updating Docker Hub repository: $NAMESPACE/$REPOSITORY"
jq -n \
  --arg description "$DESCRIPTION" \
  --rawfile full_description "$README_PATH" \
  '{description: $description, full_description: $full_description}' |
curl -fsS \
  -H "Content-Type: application/json" \
  -H "Authorization: JWT $JWT" \
  -X PATCH \
  -d @- \
  "https://hub.docker.com/v2/repositories/$NAMESPACE/$REPOSITORY/" >/dev/null

echo "Done. Docker Hub description updated: $NAMESPACE/$REPOSITORY"
