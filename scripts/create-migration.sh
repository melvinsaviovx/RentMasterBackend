#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

NAME="${1:-InitialCreate}"

dotnet tool restore
dotnet restore RentMaster.sln
dotnet build RentMaster.sln --no-restore

dotnet ef migrations add "$NAME" \
  --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj \
  --startup-project src/RentMaster.Api/RentMaster.Api.csproj \
  --context AppDbContext \
  --output-dir Persistence/Migrations \
  --no-build
