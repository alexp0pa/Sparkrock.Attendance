# 0004. Saving daily attendance

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Legacy saves the whole class through one call to `sp_SaveDailyAttendance` (`legacy/Forms/frmDailyAttendance.frm:116-117`), but the save has serious flaws:
- **Silent present default:** any student left blank is saved as Present (`frmDailyAttendance.frm:90, 109`).
- **Today only:** the date is always today, so past days can't be corrected (`frmDailyAttendance.frm:40`).
- **Partial saves:** there is no transaction (`sp_SaveDailyAttendance.sql:40-117`).
- **Duplicates:** there is no uniqueness on student + date (`schema.sql:21-36`).
- **Wrong-student overwrite:** a variable that is never reset can overwrite another student's row (`sp_SaveDailyAttendance.sql:54-57`).

See `docs/legacy-analysis.md` (R1–R5, B1, B3, B4, B10).

## Decision

- A single **PUT** endpoint saves attendance for one date and multiple students in **one database transaction, all or nothing**.
  - If any record is invalid, the whole request is rejected with 400 and nothing is written.
  - The request may not list the same student twice.
- At most **one record per student per date**, enforced by a unique index on (StudentId, Date). Sending the same request again overwrites the existing records, so it can safely be repeated.
- **Every student needs an explicit status.** There is no default to Present. A "mark all present" shortcut belongs in the client, not the API.
- **Allowed dates:** any date in the current school year (ADR 0005) up to and including today. Future dates are rejected.

## Alternatives considered

- **Present by default, as in legacy.** Rejected because an unmarked student would be silently recorded as present.
- **One request per student.** Rejected because it allows partial class saves, which is the legacy bug B3.
- **Today only, as in legacy.** Rejected because teachers need to correct past days.

## Consequences

- Legacy bugs B1, B3 and B4 cannot occur by design.
- "Today" comes from `IDateTimeProvider` (UTC). The school's time zone is not modelled; revisit this if schools in different time zones use the system.
- There are no checks for weekends or holidays, because no school calendar exists. This matches legacy.
- Corrections overwrite the existing record. Only the latest change is audited (ADR 0007).
