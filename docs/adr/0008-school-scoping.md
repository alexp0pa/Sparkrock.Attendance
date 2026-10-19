# 0008. School scoping (no school entity)

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

Legacy refers to schools only by an integer `SchoolID`. That column has no foreign key, and the `Schools` table is not part of the legacy sources (`legacy/Database/schema.sql:3, 24`). The new system has no school table and must not introduce a School entity.

## Decision

Keep the legacy behaviour:
- **School ID:** it is a plain `int` with no entity and no foreign key. It arrives with the save request and is stored on each attendance record without validation (`legacy/Database/sp_SaveDailyAttendance.sql:2, 75`).
- **Uniqueness:** a student has at most one record per date, whatever the school. Saving from another school overwrites that record, including its school ID (`sp_SaveDailyAttendance.sql:54-56`).
- **History:** a student's history covers all schools (`legacy/Database/sp_GetStudentAttendance.sql:29`).
- **Chronic absenteeism:** absences are counted per student, per school and per school year (`sp_SaveDailyAttendance.sql:79-84`).

## Alternatives considered

- **A School entity.** Rejected because it is not available.
- **Count chronic absences across all schools.** Rejected because the user chose to keep the legacy behaviour.

## Consequences

- Nothing checks that a school ID exists or that a student belongs to that school.
- A student who changes schools mid-year has their absences counted separately per school, so they may stay below the threshold at each school.
- The threshold and the school-year start are global settings (ADR 0005, ADR 0006).
