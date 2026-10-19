# 0003. Fixed attendance statuses and tardiness

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Legacy uses a configurable `AttendanceCodes` table, but no codes are defined in it (`legacy/Database/schema.sql:12-19`). Each attendance row stores a copy of the code's absent/excused flags, and those copies can drift from the code definitions (`legacy/Database/sp_SaveDailyAttendance.sql:46-53, 61-62`). Unknown codes are accepted and treated as present, and they then disappear from history (`legacy/Database/sp_GetStudentAttendance.sql:27`).

Tardiness exists only as a free `MinutesLate` number. It is not tied to any code and is never counted (`legacy/Database/sp_SaveDailyAttendance.sql:63`; `legacy/Forms/frmDailyAttendance.frm:112`). See `docs/legacy-analysis.md` (R6, R7, B2, B11, B13).

## Decision

- Attendance status is a fixed set: `Present`, `Late`, `AbsentExcused`, `AbsentUnexcused`. Whether a status counts as absent or excused follows from the status itself; no flags are stored.
- `MinutesLate` is required and must be greater than 0 when the status is `Late`. It must be empty for every other status.
- Late arrivals do not count toward chronic absenteeism (see ADR 0006).

## Alternatives considered

- **A configurable code table, as in legacy.** Rejected: the legacy codes are unknown, the copied flags drift from the codes, and a fixed set makes the rules explicit and testable.

## Consequences

- Any other status value is rejected with a validation error (400), so unknown codes can no longer be saved.
- Adding a status later requires a code change and an ADR.
- Codes from legacy data (`P`, …) would need a mapping if data is ever migrated; migration is out of scope.
