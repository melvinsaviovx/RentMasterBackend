# UAT release notes

## Authentication and presentation

- Removed all visible test-account shortcuts, seeded Owner/Tenant accounts and hard-coded UI credentials.
- Replaced the oversized login lockup with a compact, consistently aligned brand treatment.
- Replaced sidebar letter abbreviations with accessible line icons.
- Reworked login and registration copy so every heading, helper and validation message explains a real user action.
- Added stronger form validation and session-only browser token storage.

## Messages

- Rebuilt the property-linked message workspace for desktop, tablet and mobile.
- Added polished conversation icons, search, unread state, timestamps, date dividers, paging and scroll preservation.
- New messages do not pull the user away while they are reading older messages.
- Added sent/read indicators and automatic eight-second polling refresh for local/Dev/UAT.
- Added workflow system messages for applications, tenancy confirmation, scheduled move-in, move-out and final handover.
- Conversation access is validated on every API operation.

## Tenancy workflow

- Added `Scheduled` status for a confirmed tenancy whose move-in date is still in the future.
- A future confirmation keeps the property `Reserved`; it is not marked occupied early.
- Added Owner-only **Record move-in handover** action on or after the agreed start date.
- A tenancy beginning today becomes `Active` immediately after Tenant confirmation.
- Pending and scheduled tenancies can be cancelled before move-in, returning the property to `Published`.
- Clarified that the preferred lease end is informational and never closes a tenancy automatically.
- Added explicit move-out request, approval and scheduled-closure states.
- Restricted final handover completion to the property Owner.
- Added a per-tenancy `hasReviewed` indicator to prevent misleading repeat-review actions.

## Reputation

- Public ratings include published reviews only.
- Added fractional star rendering, full-dataset rating distribution and category averages.
- Disputed reviews are removed immediately from every public aggregate.
- Added Admin disputed-review queue with restore/remove decisions and mandatory resolution notes.
- Reviews remain linked to completed tenancies and one review per party per tenancy is enforced.

## Identity verification

- Aadhaar and Passport remain alternatives; only one approved document is required.
- Added duplicate-document protection across pending and verified accounts.
- Resubmissions now appear using their latest submission time.
- Replaced-document cleanup is best effort and can no longer leave the database pointing to a deleted newly uploaded file.
- Admin document access remains separated from Owner/Tenant visibility.

## Data and configuration

- Admin setup uses .NET user-secrets through `scripts/configure-local-admin.sh`.
- Property removal is a safe inactive-state transition. Publishing changes that make a property unavailable close open applications and add a workflow message to existing conversations.
- Property updates require verified Owner status.
- The smoke-test script safely JSON-encodes credentials and verifies same-day tenancy activation.
