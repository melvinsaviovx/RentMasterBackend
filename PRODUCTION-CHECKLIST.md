# Rent Master release checklist

This project includes the complete local and UAT flows for owners, tenants and authorised reviewers. Complete every item below before making the site publicly available.

## Build and database

1. Install the .NET version listed in `global.json`.
2. Run `dotnet restore RentMaster.sln`.
3. Run `dotnet build RentMaster.sln --configuration Release --no-restore`.
4. Review the generated database update script before applying it to the live database.
5. Back up the database before every update.

## Private settings

1. Copy the values from `appsettings.Production.example.json` into protected hosting settings.
2. Use separate, randomly generated values for the signing key and identity-number pepper.
3. Keep private files and data-protection keys on persistent, backed-up storage.
4. Never commit live passwords or secrets to source control.
5. Set the exact public site address in `AllowedOrigins` and `AllowedHosts`.

## Required verification

1. Run the full UAT checklist with separate Admin, Owner and Tenant accounts.
2. Confirm that complete Aadhaar and Passport numbers are visible only to the submitting user and authorised reviewers.
3. Confirm that property photos cannot be opened by unrelated users when a listing is private.
4. Confirm that a property cannot be published without a photo.
5. Confirm that reviews are available only after a tenancy is completed.
6. Confirm that support requests are visible only to the user who raised them and authorised reviewers.
7. Run an independent security review before public launch.
8. Obtain legal and privacy-policy review for identity-document handling in India.

## Hosting

1. Use HTTPS only.
2. Keep the application behind a trusted reverse proxy.
3. Store logs without request bodies or identity-document numbers.
4. Configure backups, uptime alerts and disk-space alerts.
5. Test restoration of the database, private files and data-protection keys.
