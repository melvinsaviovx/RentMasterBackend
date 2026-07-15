# Rent Master Phase 1 — Logic Review

## 1. Core identity model

Do not create separate login tables for owners and tenants.

Use one Identity user plus roles:

- a user can have one or more roles;
- role-specific profile tables hold owner-only or tenant-only data;
- future roles can be added without changing the authentication model.

## 2. Identity documents

The original idea says Aadhaar and passport are entered while logging in. This should be changed:

- documents are submitted during onboarding/KYC;
- login uses email/phone and password;
- raw identity values are never returned;
- raw numbers are not stored in SQL;
- files are private;
- the public profile contains only verification status;
- access is limited to Admin/Moderator;
- every access is audited.

Consent version, time, and IP are recorded for each identity submission. The required document list is configuration-driven. Requiring both Aadhaar and passport should be reviewed for necessity and proportionality before a real launch.

## 3. Property rules

- Only a verified Owner can create a property.
- An owner can create unlimited properties in Phase 1.
- Search responses do not expose the exact street address.
- A property cannot have more than one open tenancy.
- An occupied property cannot be manually marked available.
- A property with an open tenancy cannot be deleted.
- Deletes are soft deletes.

## 4. Tenancy rules

A review is trustworthy only when the platform can prove the relationship.

- A registered Tenant may search published houses without KYC.
- A verified Tenant applies to a property.
- The verified Owner accepts an application, which creates a pending tenancy.
- The selected Tenant must confirm.
- Either party can request closure.
- The other party must confirm closure.
- Reviews are unlocked only after closure.

Before sharing reputation, the user gives the other party a random public profile code. The application never supports reputation searches by Aadhaar, passport, phone, or email.

Future phases can add rental agreements, rent receipts, renewal, notice periods, and owner/tenant organization accounts.

## 5. Review rules

- One review per reviewer per tenancy.
- Both parties can review independently.
- Role-specific categories prevent confusing questions.
- The reviewed person cannot see identity-document data.
- Reviews require moderation before publication.
- Public output hides reviewer identity and exact property.
- The subject can dispute a published review.
- Reviews are not hard-deleted to preserve moderation history.

## 6. Abuse and fairness controls

Phase 1 includes basic prevention but production also needs:

- prohibited-content policy;
- defamation and harassment moderation;
- evidence upload for disputes;
- appeal timelines;
- reviewer anti-retaliation controls;
- account and device abuse detection;
- duplicate-account detection with privacy-preserving methods;
- review edits with version history;
- deletion/retention rules;
- grievance handling.

## 7. Security decisions

- ASP.NET Core Identity hashes passwords.
- JWT validates issuer, audience, signature, and lifetime.
- Access tokens are short-lived.
- Refresh tokens are random, hashed in the database, rotated, and revocable.
- Authentication is required by default through a fallback policy.
- Register/login/refresh are explicitly anonymous and rate limited.
- Business operations use server-side user claims, never a body `UserId`.
- CORS accepts configured origins only.
- Audit logs avoid request and response bodies.
- Database updates use `rowversion` where relevant.
- API errors return safe Problem Details.
- Secrets are loaded from user-secrets, environment settings, or Key Vault.
