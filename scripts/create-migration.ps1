param([string]$Name = "InitialCreate")
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

dotnet tool restore
dotnet restore RentMaster.sln
dotnet build RentMaster.sln --no-restore
dotnet ef migrations add $Name `
  --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj `
  --startup-project src/RentMaster.Api/RentMaster.Api.csproj `
  --context AppDbContext `
  --output-dir Persistence/Migrations `
  --no-build
