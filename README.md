# Rent Master Backend — Phase 1 (MSSQL)

Complete backend starter for a house-rental platform where Owners list houses, Tenants search and apply, both parties complete private KYC, verified tenancies are recorded, and both parties can review each other only after a tenancy ends.

## Stack

- ASP.NET Core Web API
- .NET 10
- Entity Framework Core 10
- Microsoft SQL Server / Azure SQL
- ASP.NET Core Identity
- JWT access tokens
- Rotating, hashed refresh tokens
- Role-based authorization
- SQL Server `rowversion` concurrency
- Rate limiting, safe exception handling, audit logging and private document storage abstraction

This solution uses **MSSQL**, not MySQL. The provider is `Microsoft.EntityFrameworkCore.SqlServer`, and the DbContext is configured with `UseSqlServer(...)`.

## Confirmed Phase 1 flow

1. A new Tenant registers using `role: Tenant`.
2. The Tenant can immediately sign in, search published houses and view public property details. KYC is not required for browsing.
3. The Tenant completes configured KYC documents before applying.
4. A verified Owner creates and publishes houses. An owner can list any number of houses.
5. The verified Tenant applies to a published house.
6. The Owner views only safe applicant information: display name, public reputation code and application details. Aadhaar, passport, phone and email are not exposed.
7. The Owner shortlists, rejects or accepts the application.
8. Accepting creates a pending tenancy. It does not occupy the property yet.
9. The selected Tenant confirms. The property becomes occupied and other open applications are closed.
10. Either party can request tenancy closure; the other party confirms it.
11. Reviews are allowed only after the tenancy ends, one review per side, with moderation and disputes.

Direct owner-to-email tenancy creation has been removed from Phase 1. A tenancy must originate from an accepted rental application.

## Project structure

```text
src/RentMaster.Api             Controllers, middleware and API startup
src/RentMaster.Application     Contracts, service interfaces and application errors
src/RentMaster.Domain          Entities and enums
src/RentMaster.Infrastructure  EF Core, Identity, JWT, storage and business services
scripts                        Migration/database helper scripts
docs                           Business and security design notes
```

## SQL Server connection strings

### Visual Studio LocalDB

Already configured in `appsettings.Development.json`:

```text
Server=(localdb)\MSSQLLocalDB;Database=RentMasterDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

### Local/full SQL Server with Windows authentication

```text
Server=localhost;Database=RentMasterDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

### SQL Server authentication or Docker

```text
Server=localhost,1433;Database=RentMasterDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True
```

### Azure SQL

Keep credentials out of source control and configure the value in App Service/Key Vault:

```text
Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=RentMasterDb;User ID=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

## Run with LocalDB

Requirements: .NET 10 SDK, Visual Studio SQL Server LocalDB, and HTTPS development certificate.

```bash
dotnet tool restore
dotnet restore
dotnet ef migrations add InitialCreate --project src/RentMaster.Infrastructure --startup-project src/RentMaster.Api --context AppDbContext
dotnet ef database update --project src/RentMaster.Infrastructure --startup-project src/RentMaster.Api --context AppDbContext
dotnet run --project src/RentMaster.Api
```

PowerShell helpers:

```powershell
./scripts/create-migration.ps1 InitialCreate
./scripts/update-database.ps1
```

## Run SQL Server in Docker

```bash
cp .env.example .env
docker compose up -d
```

Then override the development connection using user-secrets:

```bash
dotnet user-secrets set --project src/RentMaster.Api "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=RentMasterDb;User Id=sa;Password=RentMaster@12345;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

## Development secrets

`appsettings.Development.json` contains local-only placeholder signing values so the solution can start locally. Replace them using user-secrets before shared testing:

```bash
dotnet user-secrets set --project src/RentMaster.Api "Jwt:SigningKey" "USE-A-RANDOM-KEY-WITH-AT-LEAST-64-CHARACTERS"
dotnet user-secrets set --project src/RentMaster.Api "Verification:NumberHashPepper" "USE-A-DIFFERENT-RANDOM-SECRET-WITH-AT-LEAST-32-CHARACTERS"
```

Never copy local development secrets into production.

## Admin for local KYC/review testing

Enable the admin seed only through user-secrets:

```bash
dotnet user-secrets set --project src/RentMaster.Api "AdminSeed:Enabled" "true"
dotnet user-secrets set --project src/RentMaster.Api "AdminSeed:Email" "admin@rentmaster.local"
dotnet user-secrets set --project src/RentMaster.Api "AdminSeed:Password" "Admin@123456"
```

## Main APIs

### Authentication

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`

### Tenant browsing

- `GET /api/v1/properties/search`
- `GET /api/v1/properties/{propertyId}`

These require login through the global authorization policy but do not require KYC.

### Rental applications

- `POST /api/v1/properties/{propertyId}/applications` — verified Tenant
- `GET /api/v1/applications/mine` — Tenant
- `GET /api/v1/properties/{propertyId}/applications` — property Owner
- `POST /api/v1/applications/{applicationId}/shortlist` — property Owner
- `POST /api/v1/applications/{applicationId}/accept` — property Owner
- `POST /api/v1/applications/{applicationId}/reject` — property Owner
- `POST /api/v1/applications/{applicationId}/withdraw` — applying Tenant

### Tenancies

- `GET /api/v1/tenancies/mine`
- `POST /api/v1/tenancies/{tenancyId}/confirm`
- `POST /api/v1/tenancies/{tenancyId}/cancel-pending`
- `POST /api/v1/tenancies/{tenancyId}/request-end`
- `POST /api/v1/tenancies/{tenancyId}/confirm-end`

### KYC, reputation and reviews

See `RentMaster.Api.http` and `docs/PHASE1-DESIGN.md`.

## MSSQL-specific data protections

- Filtered unique index prevents more than one active application by the same Tenant for the same property.
- Filtered unique index prevents more than one open tenancy for a property.
- Acceptance uses a serializable SQL transaction.
- SQL Server duplicate-key errors `2601`/`2627` are converted into safe conflict responses.
- `rowversion` is used for optimistic concurrency on domain entities.
- Decimal rent/deposit fields use `decimal(18,2)`.

## Important security notes

- Raw Aadhaar/passport numbers are not stored in SQL. Only a keyed HMAC hash and masked last four characters are stored.
- Identity files are private and never exposed to Owners or Tenants.
- Replace local document storage with a private Blob provider before production.
- Enable confirmed email/phone flows before public release.
- Use Key Vault/App Service settings for SQL, JWT and verification secrets.
- Add malware scanning, retention/deletion workflows, privacy policy, consent records and human moderation before production.
- Put the API behind Azure Front Door/WAF and restrict direct App Service access.

The environment used to package this repository did not include the .NET SDK, so run `dotnet restore`, migration creation and `dotnet build` locally before merging or deployment.
