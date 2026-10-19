# 0005. School year boundaries

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Legacy hardcodes September 1 as the start of the school year and repeats that rule in three places (`legacy/Database/sp_SaveDailyAttendance.sql:32-35`, `legacy/Database/sp_GetStudentAttendance.sql:10-13, 31-33`). A school year is labelled `'YYYY-YYYY'` (`legacy/Database/schema.sql:42`).

The new system has no school table (ADR 0008), so school-year dates cannot be set per school.

## Decision

- The school-year start is **one global setting**: a month and a day, defaulting to September 1, the legacy value. A school year runs from that date up to the day before the same date in the next year.
- The rule is implemented **once**, in a single place in code, and is used by saving, history and chronic absenteeism alike.
- School years keep the legacy label format `'YYYY-YYYY'`.

## Alternatives considered

- **Per-school start and end dates.** Rejected because there is no school table to hold them (ADR 0008).
- **A hardcoded September 1, exactly as in legacy.** Rejected because it can't be adjusted without a code change.

## Consequences

- All schools share the same school-year boundaries.
- Date filters use ranges (`>= start AND < next start`) on the attendance date, so an index can be used. This fixes legacy bug B14.
- Changing the start date reassigns existing records near the boundary to a different school year, so change it only between school years.
