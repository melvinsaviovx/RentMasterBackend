# KYC document review guide

## Who reviews identity documents?

Only authenticated users with one of these roles can access the moderation API:

- `Admin`
- `Moderator`

The API controller is protected by:

```csharp
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Moderator}")]
```

Owners and Tenants can submit and view the status of their own documents, but they cannot open anyone's uploaded KYC file.

## Local development reviewer account

The Development configuration seeds this local-only Admin account when the API starts:

```text
Email: admin@rentmaster.local
Password: RentMasterAdmin@2026!
```

This is for local testing only. The base `appsettings.json` keeps Admin seeding disabled. Replace or remove the development password before sharing/deploying the application.

## Complete review flow

1. Start the API and Angular frontend.
2. Register an Owner or Tenant account.
3. Submit Aadhaar and Passport from **Identity verification**.
4. Log out.
5. Sign in with the local Admin account.
6. Open **KYC moderation** in the sidebar.
7. Select **Review document**.
8. Inspect the protected PDF/JPEG/PNG preview.
9. Verify:
   - the file is readable;
   - the selected document type is correct;
   - the identity details appear internally consistent;
   - the visible number ending matches the masked ending displayed by Rent Master.
10. Choose:
    - **Approve** — status becomes `Verified`;
    - **Reject** — a specific rejection reason is mandatory.
11. Sign back in as the Owner/Tenant and open **Identity verification** to see the result.

Both configured required document types must be `Verified` before `IsComplete` becomes `true`.

## Moderation endpoints

```text
GET  /api/v1/admin/verification/pending
GET  /api/v1/admin/verification/{documentId}/file
POST /api/v1/admin/verification/{documentId}/decision
```

Decision body examples:

```json
{
  "approve": true,
  "reason": null
}
```

```json
{
  "approve": false,
  "reason": "The image is blurred. Upload a clear, full-page copy."
}
```

## Security behaviour

- File endpoints require the `Admin` or `Moderator` role.
- Responses use `Cache-Control: no-store` and `Pragma: no-cache`.
- File access and decisions pass through the API audit middleware.
- Document numbers are normalised and stored as an HMAC hash plus the last four characters.
- Duplicate verified identity numbers cannot be linked to a different account.
- Rejected documents can be replaced; approved documents cannot be overwritten.
- Uploaded file signatures are checked for PDF, JPEG and PNG content.

## Production requirements

Before production, replace local file storage with private encrypted object storage, add malware scanning, define retention/deletion rules, require reviewer MFA, remove seeded passwords, and establish a documented manual-verification policy.
