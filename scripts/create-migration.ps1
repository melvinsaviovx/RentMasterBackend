param(
    [string]$Name = "InitialCreate"
)

$ErrorActionPreference = "Stop"

dotnet tool restore
dotnet restore
dotnet ef migrations add $Name `
  --project src/RentMaster.Infrastructure `
  --startup-project src/RentMaster.Api `
  --context AppDbContext
