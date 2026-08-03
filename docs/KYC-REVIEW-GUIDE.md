# Local KYC review guide

## Purpose

Rent Master accepts Aadhaar or Passport as alternative identity documents. One approved document completes verification.

## Configure the Admin

```bash
./scripts/configure-local-admin.sh
./scripts/run-api.sh
```

The script stores the Admin email and password in .NET user-secrets. No credentials are committed to the repository or displayed on the login page.

## Review flow

1. Register an Owner or Tenant through the frontend.
2. Open **Identity verification**.
3. Select Aadhaar or Passport.
4. Enter the document number and consent.
5. Upload a PDF, JPEG or PNG within the configured size limit.
6. Sign in as Admin.
7. Open **KYC moderation**.
8. Preview the pending document.
9. Approve it, or reject it with a specific correction reason.
10. The applicant sees the updated status and may resubmit a rejected document.

## UAT checks

- Other Owners and Tenants cannot open the uploaded file.
- The public UI displays verification status, not the full document number.
- Unsupported file types and oversized files are rejected.
- A rejection requires a reason.
- Approval of either accepted document type marks the user verified.
- The same document value cannot be reused across accounts.
