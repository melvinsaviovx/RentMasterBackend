# Rent Master - Deployment Experience Upgrade

## Verification mode

`Verification:AutoApproveSubmissions` controls the identity flow.

- `true`: intended for local/public demo use. A valid Aadhaar/Passport-form submission is marked Verified immediately so a visitor can continue to protected rental features without waiting for an admin. Demo document hashes are scoped per account so different visitors can reuse the same sample value.
- `false`: normal workflow. Submissions remain Pending until Admin/Moderator approval, and duplicate identity-document protection remains cross-account.

Current defaults:

- `appsettings.Development.json`: `true`
- `appsettings.json`: `false`
- `appsettings.Production.example.json`: `false`

For a hosted demo, prefer an environment variable instead of editing committed production configuration:

`Verification__AutoApproveSubmissions=true`

Do not use real Aadhaar/Passport data in a public demo environment. Auto-approval validates the configured input/file rules; it is not government identity verification.

## Maintenance role

Maintenance is not self-registerable. Admin creates a Maintenance login. The role is intentionally restricted.

Flow:

1. Owner or Tenant raises a maintenance request against an active/upcoming tenancy.
2. Owner or Admin assigns the request to an active Maintenance account.
3. Maintenance user sees only jobs assigned to that account.
4. Maintenance user moves Assigned -> In Progress -> Completed and can add a work note.
5. Owner confirms the completed work and closes the request. Admin can oversee the queue.

Maintenance users do not receive Owner/Tenant property, application, tenancy-management, identity-review or reputation permissions.

## Chat state

- `Sent`: stored by the API, not yet received by the recipient inbox.
- `Delivered`: recipient's chat inbox has loaded the message.
- `Read`: recipient opened the conversation and the read endpoint completed.
- `Last seen`: updated from chat activity and displayed at the top of the conversation. Recent activity is shown as Online.

The existing 8-second frontend refresh remains in place, so this is polling-based delivery/presence rather than websocket presence.

## Database migration

New migration: `20260815090000_DeploymentExperienceUpgrade`

It adds:

- `AspNetUsers.LastSeenAtUtc`
- `ChatMessages.DeliveredAtUtc`
- `MaintenanceRequests`

Run the project's normal EF migration process before using the new features. If automatic migrations are enabled for the environment, the migration will be applied on startup by the existing application setup.

## Validation performed in this workspace

- All backend JSON configuration files parsed successfully.
- 116 C# source files passed lexical delimiter validation.
- Cross-checked new DTO construction sites, role seeding, DI registration, routes and migration wiring.
- A full `dotnet build` could not be executed because the .NET SDK is not installed in this execution environment.

Use a machine with the .NET 10 SDK for the final compile/database integration check before production deployment.

## Frontend validation note

- 47 TypeScript files passed parser-level syntax validation.
- 8 frontend project JSON files parsed successfully.
- The Angular production build could not be completed in this workspace because the required npm dependency set could not be restored from the npm registry. Run `npm ci` followed by `npm run build` in a network-enabled environment before production deployment.
