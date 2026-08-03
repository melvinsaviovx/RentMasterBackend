# Rent Master — Review Workflow and UI Release

## Main corrections

- Reviews are available only after both parties approve the tenancy closure and the owner records the final handover.
- Each owner and tenant can publish one review for the completed tenancy.
- New reviews are published immediately; there is no waiting-for-approval queue.
- The reviewed person can report a review with a reason.
- Reporting a review does not automatically remove it or change the public rating.
- Admin can inspect reported reviews and either keep or remove them with a mandatory decision note.
- Existing reviews that were waiting or hidden under the previous workflow are returned to the published state when the latest database changes are applied.
- The logged-in user's reputation profile is loaded directly from their account, preventing stale profile-code failures.
- Reputation errors no longer display both a generic error and “Profile not found” at the same time.
- Help & Support has a redesigned header, clearer questions, a properly spaced request form and a cleaner request-history area.
- The main content now fills the available page height, keeping the AvioInfotech footer at the bottom instead of midway through short pages.
- Property photo upload, full identity-number display for authorised reviewers, verified-user lists and Help & Support remain included.
- Copyright remains: © 2026 AvioInfotech. All rights reserved.

## Required local steps

### Frontend

```bash
cd RentMasterFrontend
rm -rf node_modules .angular
npm ci
ng serve
```

### Backend

```bash
cd RentMasterBackend
rm -rf src/*/bin src/*/obj
dotnet restore RentMaster.sln
dotnet build RentMaster.sln
dotnet ef database update \
  --project src/RentMaster.Infrastructure/RentMaster.Infrastructure.csproj \
  --startup-project src/RentMaster.Api/RentMaster.Api.csproj
cd src/RentMaster.Api
dotnet run
```

## Review workflow test

1. Complete a tenancy closure request.
2. Approve the closure from the other account.
3. Record the final handover as the owner.
4. Publish one owner-to-tenant review and one tenant-to-owner review.
5. Confirm both appear immediately in the relevant reputation profiles.
6. Report one review from the reviewed person's account.
7. Confirm the review remains published until an Admin decision.
8. Open Admin → Reported reviews.
9. Test “Keep review” and “Remove review”, including the required decision note.

The included `scripts/uat-smoke-test.sh` now checks immediate publication and confirms that reporting alone does not remove a review.
