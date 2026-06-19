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
UPDATE_OK=0
for AUTH_SCHEME in JWT Bearer; do
  UPDATE_STATUS="$(
    jq -n \
      --arg description "$DESCRIPTION" \
      --rawfile full_description "$README_PATH" \
      '{description: $description, full_description: $full_description}' |
    curl -sS \
      -o /tmp/frpc-manager-dockerhub-update-response.txt \
      -w "%{http_code}" \
      -H "Content-Type: application/json" \
      -H "Authorization: $AUTH_SCHEME $JWT" \
      -X PATCH \
      -d @- \
      "https://hub.docker.com/v2/repositories/$NAMESPACE/$REPOSITORY/"
  )"

  if [[ "$UPDATE_STATUS" -ge 200 && "$UPDATE_STATUS" -lt 300 ]]; then
    echo "Updated with $AUTH_SCHEME authorization."
    UPDATE_OK=1
    break
  fi

  if [[ "$UPDATE_STATUS" == "403" ]]; then
    echo "$AUTH_SCHEME authorization was denied. Trying the next authorization scheme..."
    continue
  fi

  echo "Docker Hub update failed with HTTP $UPDATE_STATUS."
  cat /tmp/frpc-manager-dockerhub-update-response.txt
  exit 1
done

if [[ "$UPDATE_OK" != "1" ]]; then
  echo "Docker Hub denied the update."
  echo "Confirm that '$USERNAME' owns or can write to '$NAMESPACE/$REPOSITORY'."
  echo "If the token is already Read & Write, Docker Hub may not allow this metadata API with your access token; update the description in the web UI or try an account password only for this API call."
  cat /tmp/frpc-manager-dockerhub-update-response.txt
  exit 1
fi

echo "Done. Docker Hub description updated: $NAMESPACE/$REPOSITORY"
