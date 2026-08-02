# Rent Master — Local/Dev/UAT Handover

## Included deliverables

- `RentMasterFrontend-UAT-Fixed.zip`
- `RentMasterBackend-UAT-Fixed.zip`
- Backend browser/API checklist: `docs/UAT-CHECKLIST.md`
- Backend automated API flow: `scripts/uat-smoke-test.sh`

## Main corrections

### UI and branding

- Removed the oversized logo from the centre of the authenticated top bar.
- Rebuilt the sidebar brand as a compact icon-and-name lockup.
- Cropped and centred the icon asset so it no longer appears offset inside its container.
- Added responsive desktop/mobile navigation and clearer role/profile information.
- Added consistent loading, empty, success, error and workflow states.

### Owner–tenant chat

- Added private property-linked conversations for Owners and Tenants.
- Added persisted message history, unread counts, read state, message timestamps and five-second local/UAT polling.
- Added workflow system messages for applications and tenancies.
- Enforced participant checks on every conversation/message API operation.
- Kept one continuous conversation for a property/tenant pair and linked it to the latest application/tenancy lifecycle.

### Logical tenancy dates

- `Preferred move-in date`: must be today or later.
- `Planned lease end`: optional planning date; it does not automatically end a tenancy.
- `Requested move-out date`: supplied by one party with a reason.
- The requester cannot approve their own move-out request.
- A future approved date produces `Move-out scheduled`; the property remains occupied.
- Closure can be completed only on or after the approved date.
- `Actual move-out` is recorded only when the closure completes.
- Calendar validation uses the India local date (`Asia/Kolkata`).

### Property/application state flow

`Published → Reserved → Occupied → Published`

- Accepting an application reserves the property.
- The property becomes occupied only after the selected Tenant confirms.
- Cancelling the pending invitation republishes the property.
- Completing tenancy closure republishes the property.
- Other pending applications close when the selected Tenant confirms, and applicants receive a system message.

### Local/UAT setup

The Development environment seeds:

- Admin: `admin@rentmaster.local` / `RentMasterAdmin@2026!`
- Owner: `owner@rentmaster.local` / `RentMasterDemo@2026!`
- Tenant: `tenant@rentmaster.local` / `RentMasterDemo@2026!`

It also creates a verified Owner, verified Tenant and a demo property in Thoraipakkam.

Aadhaar **or** Passport approval is sufficient. Verification remains a manual Admin/Moderator action for local/UAT.

## Run locally

### Backend

From the backend root:

```bash
chmod +x scripts/*.sh
./scripts/setup-local.sh
./scripts/run-api.sh
```

Manual alternative:

```bash
docker compose up -d
dotnet restore RentMaster.sln
dotnet build RentMaster.sln
dotnet ef database update \
  --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj \
  --startup-project src/RentMaster.Api/RentMaster.Api.csproj \
  --context AppDbContext
dotnet run --project src/RentMaster.Api/RentMaster.Api.csproj
```

The default Docker and Development connection-string password are aligned to `MyPassword@123`.

### Frontend

From the frontend root:

```bash
npm install
npm start
```

Open `http://localhost:4200`.

### Automated API workflow

While the API is running:

```bash
./scripts/uat-smoke-test.sh
```

## Validation completed in this workspace

- All JSON files parsed successfully.
- All 38 TypeScript files passed syntax transpilation.
- All local TypeScript import paths resolved.
- CSS lexical/bracket validation passed.
- All 80 C# source files passed lexical/bracket validation.
- Migration metadata, filtered indexes and shell-script syntax were checked.
- ZIP contents were cleaned of generated caches/build output.

## Runtime validation limitation

A full Angular build and .NET build/API execution could not be run in this workspace because the .NET SDK is not installed and the available npm mirror does not contain every Angular transitive package. Run the commands above on the Mac before UAT. The included smoke script is designed to verify the complete API workflow after the local build succeeds.
