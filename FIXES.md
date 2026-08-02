# Corrected backend notes

This package includes the complete Phase 1 source and the following corrections:

- Fixed invalid `FrameworkReference` placement in the Infrastructure project.
- Fixed the nullable reputation-average compilation error.
- Added a macOS-compatible EF Core design-time `DbContext` factory.
- Aligned Docker SQL Server and development connection settings.
- Pinned the .NET SDK and the local `dotnet-ef` tool.
- Updated .NET/EF packages to the current .NET 10 servicing version used by this package.
- Updated `Microsoft.OpenApi` to a non-vulnerable patched version.
- Added build, migration, database-update, run and one-command setup scripts.
- Hardened validation, private document-path handling, middleware error responses and security headers.

## Important validation note

The artifact environment used to prepare this package does not provide a .NET SDK, so a real `dotnet build` could not be executed here. XML, JSON, shell syntax, source delimiters and all previously reported regression points were validated statically. Run `./scripts/setup-local.sh` on the Mac to perform the authoritative restore, build, migration and database update using the pinned SDK/tool versions.

## KYC moderation and duplicate-card fix

- Removed default Aadhaar/Passport values from `VerificationOptions` so configuration binding cannot append duplicates.
- Added startup validation that rejects duplicate configured document types.
- Added a defensive distinct required-document collection in the verification service.
- Added reviewer-visible applicant name/email to the pending KYC DTO.
- Added `ReviewedAtUtc` to the user's document-status response.
- Enabled a local-only Development Admin seed for end-to-end KYC testing.
- Added `docs/KYC-REVIEW-GUIDE.md` with the complete review process.
