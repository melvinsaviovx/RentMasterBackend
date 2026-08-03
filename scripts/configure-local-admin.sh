#!/usr/bin/env bash
set -euo pipefail

PROJECT="src/RentMaster.Api/RentMaster.Api.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet was not found. Install the .NET 10 SDK first." >&2
  exit 1
fi

read -r -p "Local/UAT admin email: " ADMIN_EMAIL
read -r -s -p "Local/UAT admin password: " ADMIN_PASSWORD
echo

if [[ -z "$ADMIN_EMAIL" || -z "$ADMIN_PASSWORD" ]]; then
  echo "Email and password are required." >&2
  exit 2
fi

if (( ${#ADMIN_PASSWORD} < 10 )); then
  echo "Password must contain at least 10 characters and satisfy the configured Identity rules." >&2
  exit 2
fi

dotnet user-secrets set --project "$PROJECT" "AdminSeed:Enabled" "true" >/dev/null
dotnet user-secrets set --project "$PROJECT" "AdminSeed:Email" "$ADMIN_EMAIL" >/dev/null
dotnet user-secrets set --project "$PROJECT" "AdminSeed:Password" "$ADMIN_PASSWORD" >/dev/null

echo "Local admin seed configured in .NET user-secrets."
echo "Start the API once to create the account, then optionally disable the seed with:"
echo "dotnet user-secrets set --project $PROJECT 'AdminSeed:Enabled' 'false'"
