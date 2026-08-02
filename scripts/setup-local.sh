#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

command -v dotnet >/dev/null 2>&1 || {
  echo "Error: .NET SDK is not installed or is not in PATH." >&2
  exit 1
}

command -v docker >/dev/null 2>&1 || {
  echo "Error: Docker Desktop is not installed or Docker is not running." >&2
  exit 1
}

if [[ ! -f .env ]]; then
  cp .env.example .env
fi

docker compose up -d

echo "Checking SQL Server container health..."
for _ in {1..30}; do
  status="$(docker inspect --format='{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' rentmaster-sqlserver 2>/dev/null || true)"
  if [[ "$status" == "healthy" || "$status" == "running" ]]; then
    break
  fi
  sleep 2
done

status="$(docker inspect --format='{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' rentmaster-sqlserver 2>/dev/null || true)"
if [[ "$status" != "healthy" && "$status" != "running" ]]; then
  echo "Error: SQL Server container did not become ready. Run: docker compose logs sqlserver" >&2
  exit 1
fi

dotnet tool restore
dotnet restore RentMaster.sln
dotnet build RentMaster.sln --no-restore

MIGRATIONS_DIR="src/RentMaster.Infrastructure/Persistence/Migrations"
if [[ ! -d "$MIGRATIONS_DIR" ]] || ! find "$MIGRATIONS_DIR" -maxdepth 1 -name '*.cs' -print -quit 2>/dev/null | grep -q .; then
  dotnet ef migrations add InitialCreate \
    --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj \
    --startup-project src/RentMaster.Api/RentMaster.Api.csproj \
    --context AppDbContext \
    --output-dir Persistence/Migrations \
    --no-build
fi

dotnet ef database update \
  --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj \
  --startup-project src/RentMaster.Api/RentMaster.Api.csproj \
  --context AppDbContext \
  --no-build

echo "Local setup completed. Start the API with: ./scripts/run-api.sh"
