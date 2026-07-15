# Rent Master Backend — Phase 1

A secure ASP.NET Core / Entity Framework Core backend for verified rental relationships, property listings, tenancy records, and two-way reputation.

## Technology

- .NET 10 LTS
- ASP.NET Core Web API
- Entity Framework Core 10
- SQL Server
- ASP.NET Core Identity
- JWT access tokens
- Rotating, hashed refresh tokens
- Role and policy-based authorization
- Built-in rate limiting
- Private identity-document storage abstraction
- Global exception handling and audit logging
- Optimistic concurrency with SQL Server `rowversion`

> The code can be retargeted to .NET 8, but .NET 10 is used because it is the current LTS baseline for a new project.

## Important product rule

Aadhaar/passport collection is **KYC onboarding**, not part of every login.

This starter never stores a raw identity number in SQL. It stores:

- document type;
- last four characters for display;
- an HMAC hash for duplicate detection;
- a private storage object name for the uploaded document;
- verification status and reviewer audit information.

Uploaded identity files are kept outside the public web root by the development storage provider. Production must replace it with a private Azure Blob container using managed identity, encryption, short-lived download access, retention rules, and access logging.

## Phase 1 business flow

1. A person registers as `Owner` or `Tenant`.
2. The person signs in and submits required identity documents.
3. An `Admin` or `Moderator` verifies or rejects each document.
4. A verified owner creates and publishes any number of properties.
5. A verified owner creates a tenancy invitation for a verified tenant.
6. The tenant confirms the relationship.
7. Either party requests closure; the other party confirms it.
8. Only after the tenancy is `Ended` can each party submit one review.
9. Reviews enter moderation and become visible only after publication.
10. A published review can be disputed, without silently deleting its history.
11. A user shares a random public reputation code; nobody is searched by Aadhaar, passport, phone, or email.

This verified-tenancy rule prevents unrelated users from creating fake ratings.

## Roles

Roles are stored through ASP.NET Core Identity rather than a fixed user-type enum. This permits future roles without changing the user table.

Seeded roles:

- `Owner`
- `Tenant`
- `Admin`
- `Moderator`

Self-registration is restricted to Owner and Tenant.

## Local setup

### 1. Requirements

- .NET 10 SDK
- SQL Server or SQL Server container
- `dotnet-ef` tool

### 2. Configure secrets

From `src/RentMaster.Api`:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=RentMasterDb;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:SigningKey" "use-at-least-64-random-characters-here"
dotnet user-secrets set "Verification:NumberHashPepper" "use-a-different-long-random-secret-here"
```

For SQL authentication:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=RentMasterDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True"
```

### 3. Create the database

From the repository root:

```bash
dotnet tool install --global dotnet-ef
dotnet restore
dotnet ef migrations add InitialCreate \
  --project src/RentMaster.Infrastructure \
  --startup-project src/RentMaster.Api
dotnet ef database update \
  --project src/RentMaster.Infrastructure \
  --startup-project src/RentMaster.Api
```

### 4. Run

```bash
dotnet run --project src/RentMaster.Api
```

Development OpenAPI document:

```text
/openapi/v1.json
```

Health check:

```text
/health
```

## Optional development admin seed

Do not commit a production admin password.

```bash
dotnet user-secrets set "AdminSeed:Enabled" "true"
dotnet user-secrets set "AdminSeed:Email" "admin@rentmaster.local"
dotnet user-secrets set "AdminSeed:Password" "a-strong-local-only-password"
```

## Main endpoints

### Authentication

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`

### Verification

- `POST /api/v1/verification/documents` — multipart form
- `GET /api/v1/verification/status`
- `GET /api/v1/admin/verification/pending`
- `GET /api/v1/admin/verification/{documentId}/file`
- `POST /api/v1/admin/verification/{documentId}/decision`

### Properties

- `POST /api/v1/properties`
- `GET /api/v1/properties/mine`
- `GET /api/v1/properties/search`
- `PUT /api/v1/properties/{id}`
- `DELETE /api/v1/properties/{id}`

### Tenancies

- `POST /api/v1/tenancies`
- `GET /api/v1/tenancies/mine`
- `POST /api/v1/tenancies/{id}/confirm`
- `POST /api/v1/tenancies/{id}/request-end`
- `POST /api/v1/tenancies/{id}/confirm-end`

### Reviews

- `POST /api/v1/reviews`
- `POST /api/v1/reviews/{id}/dispute`
- `GET /api/v1/reputation/{profileCode}`
- `GET /api/v1/admin/reviews/pending`
- `POST /api/v1/admin/reviews/{id}/decision`

## Review categories

Owner reviewing tenant:

- `RentPayment`
- `PropertyCare`
- `NeighbourConduct`
- `Communication`

Tenant reviewing owner:

- `MaintenanceResponse`
- `Communication`
- `PrivacyRespect`
- `DepositFairness`

The category model is stored in a child table so future categories do not require new review columns.

## Security checklist before production

- Replace development document storage with private Azure Blob Storage.
- Put JWT and document-hash secrets in Azure Key Vault/App Service settings.
- Enable email verification and password reset mail.
- Configure exact production CORS origins.
- Put the API behind Azure Front Door/WAF.
- Configure trusted proxy/forwarded-header rules.
- Add malware scanning for uploaded files.
- Add document retention/deletion workflows and consent history.
- Add review abuse reporting, appeal SLAs, and a human moderation policy.
- Add privacy policy, terms, grievance contact, and legal review.
- Never expose raw Aadhaar/passport details to owners, tenants, logs, analytics, or API responses.
- Never use Aadhaar/passport number as the public account identifier.
- Add integration tests before deployment.
