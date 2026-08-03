# Rent Master end-to-end correction notes

Release date: 3 August 2026

## What was wrong

### 1. Reputation page failed for valid profile codes
The earlier reputation calculation loaded ratings, category scores and reported-review records together. That data shape could fail before a profile was returned, including for users with no reviews.

The reputation flow now:
- Finds the account from the public profile code.
- Loads published reviews separately.
- Loads category ratings separately.
- Loads unresolved reports separately.
- Returns a valid new-profile state when there are no reviews.
- Keeps a reported review visible and marks it as **Reported for review**.

### 2. Empty owner messages looked like a broken page
An owner has no conversation until a tenant selects **Message owner** or submits an application. The empty page did not explain this clearly.

The Messages page now shows role-specific guidance:
- Owners are told that tenant conversations appear after a message or application.
- Tenants are directed to an available property and **Message owner**.
- One conversation is reused through property enquiry, application and tenancy.

### 3. Tenant search showed zero homes without explaining why
Only valid homes should be visible to tenants. A home appears only when it:
- Is published by the owner.
- Has at least one photo.
- Has valid rent, address and room details.

The search page now starts without hidden filters and distinguishes:
- No homes currently published.
- No homes matching the selected filters.

The owner property list now says whether each home is visible, needs a photo, is ready to publish, reserved, occupied or inactive.

### 4. Reported-review behaviour contradicted the intended flow
A review should publish immediately after the completed tenancy. Reporting it must not automatically hide it.

The corrected flow is:
1. Both parties approve tenancy closure.
2. The owner records final handover.
3. The tenancy becomes completed.
4. Each party may publish one review.
5. The review is visible immediately.
6. The reviewed person may report a genuine concern.
7. The review remains visible and continues to count.
8. An authorised reviewer keeps or removes it with a recorded reason.

### 5. Verified-user loading was fragile
The verified-user list now loads approved accounts and documents first, then groups them safely. It supports a user with either Aadhaar or Passport and displays the complete submitted number only to the authorised reviewer and the submitting user.

## Other included behaviour

- Property photo upload: 1–10 JPG, PNG or WEBP files, maximum 8 MB each.
- A home cannot be published without at least one photo.
- Owner–tenant conversation history, unread counts, sent/read states and workflow updates.
- Manual identity review with waiting and verified-user sections.
- Help & Support with request history and reviewer replies.
- Copyright: **© 2026 AvioInfotech. All rights reserved.**
- No demo account shortcuts in the sign-in screen.
- Customer-facing pages avoid development terminology.

## Important honesty note

The source was checked for TypeScript syntax, missing local imports, missing template methods, malformed JSON, shell-script syntax, migration attribute duplication, C# bracket structure and ZIP integrity. A full Angular installation and .NET build could not be executed in this workspace because its package source is missing one Angular dependency and the .NET SDK is not installed here. Run the commands in the accompanying checklist on the target Mac before distributing the build.
