# Rent Master local/UAT checklist

## Seeded accounts

| Role | Email | Password |
|---|---|---|
| Admin | `admin@rentmaster.local` | `RentMasterAdmin@2026!` |
| Owner | `owner@rentmaster.local` | `RentMasterDemo@2026!` |
| Tenant | `tenant@rentmaster.local` | `RentMasterDemo@2026!` |

The owner and tenant are manually marked verified and a demo Thoraipakkam property is created only in the Development environment.

## Start locally

1. Start SQL Server using the included `docker-compose.yml`, or update `appsettings.Development.json` for your local SQL Server.
2. Run the API:
   ```bash
   cd src/RentMaster.Api
   dotnet restore
   dotnet ef database update --project ../RentMaster.Infrastructure/RentMaster.Infrastructure.csproj --startup-project RentMaster.Api.csproj
   dotnet run
   ```
3. Run the Angular UI in the frontend folder:
   ```bash
   npm install
   npm start
   ```
4. Open `http://localhost:4200`.

`Database:ApplyMigrationsOnStartup` is enabled in Development, so `dotnet run` also applies pending migrations. The explicit database-update command is useful when troubleshooting.

## Automated API smoke test

With the API running at `http://localhost:5085`:

```bash
./scripts/uat-smoke-test.sh
```

The script creates an isolated property and validates:

1. Owner and tenant login
2. Property creation and publishing
3. Tenant opens chat and sends a message
4. Tenant submits an application
5. Owner shortlists and accepts it
6. Accepted application reserves the property
7. Tenant confirms the tenancy and property becomes occupied
8. Owner replies in chat
9. Tenant requests move-out
10. Owner approves closure
11. Actual end date, republished property, and ended status
12. Workflow system messages in the same conversation

## Browser test flow

### Tenant

- Use the Tenant demo account button on the login page.
- Search and open the demo property.
- Confirm that the exact street address is not public.
- Click **Message owner**, send a message, refresh, and confirm history remains.
- Submit an application with today/future move-in.
- Verify a planned lease end is optional and cannot be on/before move-in.

### Owner

- Sign in as Owner.
- Open **My properties → Applications**.
- Confirm applicant Aadhaar/passport, phone, and email are not shown.
- Shortlist, chat, and accept one application.
- Confirm that Accept creates a pending invitation, changes the property to **Reserved**, and does not yet mark it occupied.

### Tenant confirmation

- Sign in as Tenant and open **Tenancies**.
- Confirm the invitation.
- Verify the property becomes occupied and other open applications close.

### Move-out logic

- On an active tenancy, request closure with a date and reason.
- Verify the requester can withdraw, but cannot approve their own request.
- As the other party, approve the request.
- For a future date, verify status becomes **Move-out scheduled** and the property stays occupied.
- On/after that date, click **Complete tenancy**.
- For today's date, approval completes immediately.
- Confirm `Actual move-out` equals the approved date, the property returns to Published, and reviews become available.

### Admin

- Sign in as Admin.
- Review KYC and review-moderation pages.
- Confirm private documents are available only in the moderation workflow.

## Expected date meaning

- **Preferred move-in:** requested by the tenant and must be today or later.
- **Planned lease end:** optional planning information from the application. It never closes a tenancy automatically.
- **Requested move-out:** the date proposed by one tenancy party.
- **Actual move-out:** recorded only after both parties approve and closure is completed.


## Reservation rule

- **Published:** visible in search and open for applications.
- **Reserved:** one application has been accepted; only linked participants can open the property details.
- **Occupied:** the selected tenant confirmed the tenancy.
- **Published again:** pending invitation is cancelled or the completed tenancy ends.

Calendar validations use the India local date (Asia/Kolkata), avoiding UTC date changes around midnight.
