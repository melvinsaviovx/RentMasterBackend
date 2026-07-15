#!/usr/bin/env bash
set -euo pipefail
dotnet tool restore
dotnet restore
dotnet ef database update \
  --project src/RentMaster.Infrastructure \
  --startup-project src/RentMaster.Api \
  --context AppDbContext
