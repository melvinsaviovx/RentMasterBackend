# Rent Master release candidate — 3 August 2026

## Included changes

- Fixed the missing `dialogMaxLength` member that stopped the frontend from starting.
- Added property-photo upload with previews, removal, file checks and a 10-photo limit.
- A property must have at least one photo before it can be published.
- Search results and property pages now show the uploaded property photos.
- Old incomplete listings without photos or realistic rent/room values are hidden from tenant search.
- Complete Aadhaar and Passport numbers are available to the submitting user and authorised identity reviewers.
- Complete identity numbers are protected while stored and are never included in normal owner/tenant profile views.
- Added an Admin Identity Review page with separate Waiting for review and Verified users lists.
- The verified-users list includes name, email, phone, role, profile code, approved document, complete number and verification date.
- Added Help & Support for users, including common questions, new support requests, status and reply history.
- Added an Admin Support Requests page for replies and resolution.
- Improved reputation loading so an unknown profile shows one clear Profile not found state instead of two conflicting errors.
- Improved rating presentation with accurate partial stars, distribution and category averages.
- Removed demo sign-in shortcuts and automatic demo Owner/Tenant accounts.
- Replaced technical wording in user-facing screens with plain language.
- Added the copyright: © 2026 AvioInfotech. All rights reserved.
- Added database updates and production configuration examples for the new features.

## Important note for existing identity records

Older versions stored only the final four characters of an identity number. The original complete number cannot be reconstructed. Those users must resubmit their identity document once. All new submissions preserve the complete number in protected storage.

## Required checks before public release

Follow the `PRODUCTION-CHECKLIST.md` files inside both projects. Run the full frontend and backend release builds, apply the database updates to a backed-up test database, and complete the Owner, Tenant and Admin journeys before publishing.
