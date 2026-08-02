$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

dotnet tool restore
dotnet restore RentMaster.sln
dotnet build RentMaster.sln --no-restore
dotnet ef database update `
  --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj `
  --startup-project src/RentMaster.Api/RentMaster.Api.csproj `
  --context AppDbContext `
  --no-build
