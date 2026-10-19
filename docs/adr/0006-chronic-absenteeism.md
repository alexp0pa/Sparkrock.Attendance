# 0006. Chronic absenteeism

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Legacy counts absent days and flags a student as chronically absent when that count reaches a threshold: `Schools.AbsenceAlertThreshold`, or 10 when that is NULL (`legacy/Database/sp_SaveDailyAttendance.sql:79-84, 98-99`; `legacy/Database/sp_GetStudentAttendance.sql:40`). Excused and unexcused absences count the same, and late arrivals don't count.

The total is stored in a summary table. That stored total drifts from the daily rows, and as written the recount covers every school year rather than one (`sp_SaveDailyAttendance.sql:83`). See `docs/legacy-analysis.md` (C1–C5, B15–B19).

## Decision

- A student is chronically absent when their **number of absent days** in a school year is at least a **fixed threshold**. This keeps the legacy rule; there is no rate and no denominator.
- The threshold is **one global setting**, `Attendance:ChronicAbsenceThreshold` in appsettings, with a default of **10**. There is no per-school value because there is no school table (ADR 0008).
- Both `AbsentExcused` and `AbsentUnexcused` count. The response reports the two counts separately as well as the total. `Late` does not count (ADR 0003).
- The flag is **computed from the daily records on every request**. Nothing is stored: there is no summary table and no alerts table.
- The count covers a single school year (ADR 0005) and follows the legacy school scoping (ADR 0008).

## Alternatives considered

- **A rate**, absent days ÷ enrolled days ≥ 10%. Rejected because the user chose to keep the legacy fixed threshold.
- **A stored summary or alerts table, as in legacy.** Rejected because the stored total drifts from the daily records (legacy bugs B15, B16, B19).

## Consequences

- The result always matches the daily records, so corrections are reflected immediately.
- The query relies on an index on (StudentId, Date).
- A count does not adjust for students who enrol late in the year; this was accepted on purpose.
- Raising, resolving and notifying alerts are out of scope.
