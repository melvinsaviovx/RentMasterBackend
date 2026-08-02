# Rent Master Backend — Phase 1

ASP.NET Core/.NET 10 backend for a house-rental platform using EF Core, SQL Server, ASP.NET Core Identity and JWT authentication.

## Implemented modules

- Owner and Tenant registration/login
- JWT access tokens and rotating hashed refresh tokens
- Role-based authorization with a global authenticated-user policy
- Private Aadhaar/passport submission and admin verification
- Owner property creation, update, listing and soft deletion
- Tenant property search and rental applications
- Owner shortlist, reject and accept workflow with Published → Reserved → Occupied property states
- Tenant tenancy confirmation with India-local calendar validation
- Owner–tenant property chat with persisted messages and workflow updates
- Date-based two-party tenancy closure with requester withdrawal and scheduled completion
- Post-tenancy owner/tenant reviews
- Review moderation and disputes
- Public reputation profiles
- Audit logging, rate limiting, CORS, health checks and safe Problem Details
- SQL Server rowversion concurrency and filtered unique indexes

## Requirements

- .NET SDK 10.0.302 or a compatible 10.0.3xx patch
- Docker Desktop, or another SQL Server instance
- macOS, Linux or Windows

## Project structure

```text
src/RentMaster.Api
src/RentMaster.Application
src/RentMaster.Domain
src/RentMaster.Infrastructure
```

## First-time setup on macOS

Run every command below from the repository root—the directory containing `RentMaster.sln`.

### Easiest setup

```bash
chmod +x scripts/*.sh
./scripts/setup-local.sh
./scripts/run-api.sh
```

The setup script starts SQL Server, restores and builds the solution, creates `InitialCreate` when no migration exists, and applies the database migration.

### 1. Start SQL Server

```bash
cp .env.example .env
docker compose up -d

docker ps
```

The default local credentials are:

```text
Server: localhost,1433
Database: RentMasterDb
User: sa
Password: MyPassword@123
```

To use another password, change both `.env` and the development connection string through user-secrets or an environment variable.

### 2. Restore tools and packages

```bash
dotnet --version
dotnet tool restore
dotnet restore RentMaster.sln
dotnet build RentMaster.sln
```

### 3. Create the initial migration

```bash
./scripts/create-migration.sh InitialCreate
```

Equivalent manual command:

```bash
dotnet ef migrations add InitialCreate \
  --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj \
  --startup-project src/RentMaster.Api/RentMaster.Api.csproj \
  --context AppDbContext \
  --output-dir Persistence/Migrations
```

### 4. Create/update the database

```bash
./scripts/update-database.sh
```

Equivalent manual command:

```bash
dotnet ef database update \
  --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj \
  --startup-project src/RentMaster.Api/RentMaster.Api.csproj \
  --context AppDbContext
```

### 5. Run the API

```bash
./scripts/run-api.sh
```

OpenAPI JSON is available in Development at:

```text
/openapi/v1.json
```

The health endpoint is:

```text
/health
```

## Configuration

Development values are in:

```text
src/RentMaster.Api/appsettings.Development.json
```

For local overrides, prefer user-secrets:

```bash
dotnet user-secrets set --project src/RentMaster.Api \
  "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=RentMasterDb;User Id=sa;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"

dotnet user-secrets set --project src/RentMaster.Api \
  "Jwt:SigningKey" \
  "USE-A-RANDOM-KEY-WITH-AT-LEAST-64-CHARACTERS"

dotnet user-secrets set --project src/RentMaster.Api \
  "Verification:NumberHashPepper" \
  "USE-A-DIFFERENT-RANDOM-SECRET-WITH-AT-LEAST-32-CHARACTERS"
```

The EF design-time factory checks `RENTMASTER_SQL_CONNECTION` first, then loads the API development settings. Example:

```bash
export RENTMASTER_SQL_CONNECTION='Server=localhost,1433;Database=RentMasterDb;User Id=sa;Password=MyPassword@123;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;'
```

## Local admin and KYC review

The Development configuration seeds local-only UAT accounts automatically:

```text
Admin: admin@rentmaster.local / RentMasterAdmin@2026!
Owner: owner@rentmaster.local / RentMasterDemo@2026!
Tenant: tenant@rentmaster.local / RentMasterDemo@2026!
```

Start/restart the API, sign in through the Angular login page, and open **KYC moderation**. The Admin can preview pending PDF/JPEG/PNG files and approve them or reject them with a mandatory correction reason.

The base `appsettings.json` keeps seeding disabled, so this account is not created in non-Development environments unless explicitly configured. Never use the development password in a deployed environment.

For the complete process and security notes, read:

```text
docs/KYC-REVIEW-GUIDE.md
```

## Main endpoints

### Authentication

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`

### Verification

- `POST /api/v1/verification/documents`
- `GET /api/v1/verification/status`
- `GET /api/v1/admin/verification/pending`
- `GET /api/v1/admin/verification/{documentId}/file`
- `POST /api/v1/admin/verification/{documentId}/decision`

### Properties and applications

- `POST /api/v1/properties`
- `GET /api/v1/properties/mine`
- `GET /api/v1/properties/search`
- `GET /api/v1/properties/{propertyId}`
- `PUT /api/v1/properties/{propertyId}`
- `DELETE /api/v1/properties/{propertyId}`
- `POST /api/v1/properties/{propertyId}/applications`
- `GET /api/v1/applications/mine`
- `GET /api/v1/properties/{propertyId}/applications`
- `POST /api/v1/applications/{applicationId}/shortlist`
- `POST /api/v1/applications/{applicationId}/accept`
- `POST /api/v1/applications/{applicationId}/reject`
- `POST /api/v1/applications/{applicationId}/withdraw`

### Tenancies and reviews

- `GET /api/v1/tenancies/mine`
- `POST /api/v1/tenancies/{tenancyId}/confirm`
- `POST /api/v1/tenancies/{tenancyId}/cancel-pending`
- `POST /api/v1/tenancies/{tenancyId}/request-end`
- `POST /api/v1/tenancies/{tenancyId}/cancel-end-request`
- `POST /api/v1/tenancies/{tenancyId}/confirm-end`
- `POST /api/v1/tenancies/{tenancyId}/complete-end`
- `POST /api/v1/reviews`
- `POST /api/v1/reviews/{reviewId}/dispute`
- `GET /api/v1/reputation/{profileCode}`
- `GET /api/v1/admin/reviews/pending`
- `POST /api/v1/admin/reviews/{reviewId}/decision`


### Chat

- `POST /api/v1/chat/properties/{propertyId}/open`
- `GET /api/v1/chat/conversations`
- `GET /api/v1/chat/conversations/{conversationId}/messages`
- `POST /api/v1/chat/conversations/{conversationId}/messages`
- `POST /api/v1/chat/conversations/{conversationId}/read`

For the complete local workflow and automated API validation, see `docs/UAT-CHECKLIST.md` and run `./scripts/uat-smoke-test.sh` while the API is running.

## Production requirements

- Replace local document storage with private encrypted Blob storage.
- Store SQL, JWT and verification secrets outside source control.
- Enable confirmed email/phone flows.
- Add malware scanning and retention/deletion policies for identity documents.
- Put the API behind a WAF/reverse proxy and configure trusted forwarded headers.
- Keep `Database:ApplyMigrationsOnStartup` disabled in production and run reviewed migrations during deployment.

## Identity verification rule

The default Phase 1 rule accepts Aadhaar or Passport. The API returns both accepted document types, but the account becomes verified after any one of them is approved by an Admin or Moderator.

```json
"Verification": {
  "RequiredDocuments": ["Aadhaar", "Passport"],
  "MinimumVerifiedDocuments": 1
}
```
