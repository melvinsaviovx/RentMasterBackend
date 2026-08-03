# Rent Master Backend

ASP.NET Core/.NET 10 API for the Rent Master local, Development and UAT rental workflow.

## Implemented modules

- Owner and Tenant registration/login
- JWT access tokens and rotating hashed refresh tokens
- Role-based authorization with an authenticated-user fallback policy
- Manual Aadhaar or Passport verification
- Duplicate identity-document protection across pending and verified accounts
- Admin KYC review with safe resubmission handling
- Property lifecycle: Draft → Published → Reserved → Occupied → Published
- Tenancy lifecycle: Pending confirmation → Scheduled or Active → Move-out scheduled → Ended
- Rental applications and tenancy invitations
- Property-linked Owner–Tenant messages with persisted history, unread counts, read state and automatic polling refresh
- Future move-in scheduling with Owner-recorded handover
- Mutually approved move-out scheduling
- Owner-recorded final handover
- Automatically published post-tenancy reviews, concern reporting, Admin decisions and public reputation totals
- Row-version concurrency, audit logging, rate limiting, CORS and health checks

No Owner or Tenant test accounts are seeded. The login UI contains no shortcut credentials.

## Requirements

- .NET SDK 10.0.302 or compatible .NET 10 SDK
- Docker Desktop or an accessible SQL Server

## Local setup

Run from the directory containing `RentMaster.sln`:

```bash
chmod +x scripts/*.sh
cp .env.example .env
./scripts/setup-local.sh
```

Configure a local Admin account in .NET user-secrets:

```bash
./scripts/configure-local-admin.sh
```

Start the API:

```bash
./scripts/run-api.sh
```

The default local API URL is:

```text
http://localhost:5085
```

Health endpoint:

```text
http://localhost:5085/health
```

OpenAPI document in Development:

```text
http://localhost:5085/openapi/v1.json
```

## Account setup for UAT

1. Configure the Admin using `scripts/configure-local-admin.sh`.
2. Start the API once so the Admin seed executes.
3. Register Owner and Tenant accounts through the Angular registration page.
4. Upload Aadhaar or Passport from each account.
5. Sign in as Admin and approve the documents.
6. Optionally disable repeated Admin seeding:

```bash
dotnet user-secrets set --project src/RentMaster.Api/RentMaster.Api.csproj "AdminSeed:Enabled" "false"
```

## Database commands

Create a migration:

```bash
./scripts/create-migration.sh MigrationName
```

Apply migrations:

```bash
./scripts/update-database.sh
```

## Automated workflow test

The smoke test expects existing verified Owner and Tenant accounts. It creates its own property, application, conversation and tenancy data.

```bash
OWNER_EMAIL='owner-email' \
TENANT_EMAIL='tenant-email' \
UAT_PASSWORD='shared-test-password' \
./scripts/uat-smoke-test.sh
```

The script validates:

- API health and authentication
- Property creation
- Property conversation and messages
- Application submission, shortlist and acceptance
- Reserved and occupied property states
- Same-day tenancy activation after confirmation
- Tenancy confirmation
- Move-out request and approval
- Owner final handover
- Property re-publication
- Workflow messages in chat history
- Immediate publication of post-tenancy reviews
- Review reporting without automatic removal from the public rating

## Tenancy date rules

- Tenant confirmation reserves a future move-in as `Scheduled`; it does not mark the property occupied early.
- On or after the agreed start date, the Owner records move-in handover and the tenancy becomes `Active`.
- A tenancy starting today becomes `Active` immediately when the Tenant confirms.
- A pending or scheduled tenancy can be cancelled before move-in and the property returns to `Published`.
- `ExpectedEndDate` is a planning preference only.
- It does not automatically close or change property availability.
- Either party may request a move-out date while the tenancy is active.
- The other party must approve the date.
- The tenancy remains occupied while move-out is scheduled.
- Only the Owner records final possession/key handover on or after the approved date.
- `ActualEndDate` is written only when final handover is completed.

## Messaging rules

- A conversation is created only in the context of a property, application or tenancy.
- Messages are persisted in SQL Server and refreshed automatically through polling.
- Every message action verifies that the signed-in user belongs to the conversation.
- System messages record application and tenancy workflow changes.

## Reputation rules

- A user can submit one review per completed tenancy after both parties approve closure and the final handover is recorded.
- A submitted review is published immediately and included in the public rating.
- The reviewed person may report a review with a specific reason.
- Reporting alone does not remove or hide the review.
- An authorised Admin may keep or remove a reported review and must record a decision note.

## Local-only configuration

Development SQL, JWT and verification secrets in `appsettings.Development.json` are local placeholders. Do not reuse them in a deployed environment. Use user-secrets locally and a managed secret provider for deployments.

This package is prepared for local/Dev/UAT validation. A production launch still requires environment-specific security, privacy, storage, monitoring, backup and deployment review.
