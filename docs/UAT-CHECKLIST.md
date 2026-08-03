# Rent Master UAT checklist

## Preparation

- [ ] SQL Server is running.
- [ ] Database migrations are applied.
- [ ] Admin is configured through `scripts/configure-local-admin.sh`.
- [ ] Frontend starts at `http://localhost:4200`.
- [ ] API health returns success at `http://localhost:5085/health`.
- [ ] Browser console has no uncaught errors during the tested flow.
- [ ] Test at desktop, tablet and mobile browser widths.

## Authentication and visual quality

- [ ] Owner registration succeeds.
- [ ] Tenant registration succeeds.
- [ ] Duplicate email and duplicate mobile number are rejected.
- [ ] Invalid password and invalid email messages are clear.
- [ ] Session refresh works and logout clears the session.
- [ ] No test credentials or account shortcuts appear on the login page.
- [ ] Login logo is compact, aligned and does not stretch or crop.
- [ ] Sidebar icons, active state, mobile menu and profile section remain aligned.
- [ ] Keyboard focus is visible on links, inputs and buttons.

## Verification

- [ ] Aadhaar or Passport may be submitted.
- [ ] Only one approved document is required.
- [ ] The same pending or verified document cannot be linked to another account.
- [ ] Admin can preview, approve and reject.
- [ ] Rejection requires a reason.
- [ ] Rejected documents can be resubmitted and show the latest submission time.
- [ ] A failed old-file cleanup does not invalidate the newly saved resubmission.
- [ ] Non-admin users cannot access private document files.

## Properties and applications

- [ ] Verified Owner can create, publish, edit and deactivate a property.
- [ ] Unverified Owner cannot create or update a property.
- [ ] Search filters reject invalid rent and bedroom values.
- [ ] Reserved, occupied and inactive properties do not accept new applications.
- [ ] Making a published property Draft or Inactive closes submitted/shortlisted applications and writes a workflow message.
- [ ] Verified Tenant can open a property conversation.
- [ ] Application message cannot contain only spaces.
- [ ] Owner can shortlist, reject and accept an application.
- [ ] Acceptance reserves the property.

## Messages

- [ ] Conversation search works by user, profile code and property.
- [ ] Text is persisted after refresh.
- [ ] Empty and over-limit messages are blocked.
- [ ] Unread count clears after opening the conversation.
- [ ] Sender sees **Sent**, then **Read** after the recipient opens the conversation.
- [ ] Earlier-message paging keeps the current scroll position.
- [ ] New messages do not force-scroll a user who is reading older messages.
- [ ] Date dividers and message times are correct in the local timezone.
- [ ] System messages appear for application and tenancy workflow changes.
- [ ] A non-participant cannot access a conversation or its messages.
- [ ] Desktop and mobile conversation layouts remain usable without horizontal overflow.
- [ ] Automatic polling updates the open conversation without duplicate messages.

## Move-in and tenancy dates

- [ ] Confirming a tenancy starting today makes it `Active` and the property `Occupied`.
- [ ] Confirming a future move-in makes it `Scheduled` and keeps the property `Reserved`.
- [ ] Owner cannot record move-in handover before the start date.
- [ ] Tenant cannot record move-in handover.
- [ ] Owner can record handover on or after the start date, making the tenancy `Active` and property `Occupied`.
- [ ] A pending or scheduled tenancy can be cancelled before move-in.
- [ ] Cancelling before move-in returns the property to `Published`.
- [ ] Preferred lease end is shown as a planning reference only.

## Move-out and closure

- [ ] Either party can request a move-out date for an active tenancy.
- [ ] The requester may withdraw before approval.
- [ ] The other party must approve the date.
- [ ] Future approved dates keep the tenancy occupied.
- [ ] Tenant cannot record final handover.
- [ ] Owner cannot record handover before the approved date.
- [ ] Owner can complete final handover on or after the approved date.
- [ ] Actual end date is set only at final handover.
- [ ] Property returns to `Published` after closure.

## Reviews and reputation

- [ ] Reviews are unavailable before tenancy closure.
- [ ] Each party can review once per tenancy.
- [ ] Duplicate review attempts are rejected.
- [ ] All overall and category ratings require values from 1 to 5.
- [ ] New reviews remain pending until moderation.
- [ ] Fractional average stars render consistently with the numeric rating.
- [ ] Published review count, average, distribution and category averages agree.
- [ ] Disputing a review immediately removes it from public totals and averages.
- [ ] Admin can restore or remove a disputed review with a reason.
- [ ] Review button changes to **Review submitted** after submission.
- [ ] Public lookup accepts profile code only—not email, phone, Aadhaar or Passport.

## Final checks before sharing

- [ ] `npm ci` passes.
- [ ] `npm run build:production` passes.
- [ ] `dotnet restore RentMaster.sln` passes.
- [ ] `dotnet build RentMaster.sln --no-restore` passes.
- [ ] `./scripts/uat-smoke-test.sh` passes with verified test accounts.
- [ ] No TODO/FIXME markers remain in application source.
- [ ] No hard-coded login credentials appear in source or browser UI.
- [ ] No `bin`, `obj`, `node_modules`, `.angular`, `.git`, `.env` or local document files are included in the handoff ZIPs.
