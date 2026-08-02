#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

dotnet tool restore
dotnet restore RentMaster.sln
dotnet build RentMaster.sln --no-restore

dotnet ef database update \
  --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj \
  --startup-project src/RentMaster.Api/RentMaster.Api.csproj \
  --context AppDbContext \
  --no-build
