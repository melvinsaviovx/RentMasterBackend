#!/usr/bin/env bash
set -euo pipefail
NAME="${1:-InitialCreate}"
dotnet tool restore
dotnet restore
dotnet ef migrations add "$NAME" \
  --project src/RentMaster.Infrastructure \
  --startup-project src/RentMaster.Api \
  --context AppDbContext
