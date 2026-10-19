# 0007. Audit columns on attendance records

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Legacy fills `CreatedBy` and `ModifiedBy` with `SYSTEM_USER`, which is the database login rather than the teacher, and takes its timestamps from server-local `GETDATE()` (`legacy/Database/sp_SaveDailyAttendance.sql:65-66, 76`).

The template already has a `User` entity (`src/Domain/Users/User.cs`). The JWT it issues carries the user's email as a claim (`src/Infrastructure/Authentication/TokenProvider.cs:26`).

## Decision

- Attendance records have the columns `CreatedBy`, `CreatedAtUtc`, `ModifiedBy` and `ModifiedAtUtc`.
- `CreatedBy` and `ModifiedBy` hold the **email claim from the JWT** of the logged-in user, the template's `User`.
- The timestamps come from `IDateTimeProvider` and are stored in UTC.
- Only the latest change is kept; there is no history table.

## Alternatives considered

- **Store the user's Guid ID.** Rejected because the user chose the email.
- **A full change-history table.** Not needed for now; it can be added in a later ADR.

## Consequences

- `IUserContext` gains an `Email` property, read from the email claim (`ClaimTypes.Email` after the inbound claim mapping). A request without that claim fails the same way a missing user ID does.
- The audit columns are bounded with `HasMaxLength(256)`.
- If a user's email changes, earlier records keep the old address. This is acceptable for an audit trail.
