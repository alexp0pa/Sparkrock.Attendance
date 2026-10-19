# 0009. Minimal Student entity

- **Status:** Accepted (SchoolId amended by 0011)
- **Date:** 2026-10-19

## Context

Legacy attendance rows hold a `StudentID` without a foreign key (`legacy/Database/schema.sql:23`), and the save procedure never checks that the student exists (`legacy/Database/sp_SaveDailyAttendance.sql:54-77`). Student roster management is out of scope, but attendance has to refer to real students, and the history and chronic list need student names.

## Decision

Add a minimal `Student` entity with an `int Id` (ADR 0002), `FirstName` and `LastName`. `AttendanceRecord.StudentId` is a foreign key to it, with delete restricted.

- Saving attendance for an unknown student rejects the whole request (400), as required by ADR 0004.
- The history of an unknown student returns 404.
- Students have no `SchoolId` and no `Active` flag. Which school a record belongs to stays on the attendance record (ADR 0008).

## Alternatives considered

- **A plain `int StudentId` with no entity, as in legacy.** Rejected: the user wants the foreign key, so attendance can't be recorded for students that don't exist.

## Consequences

- There is no API for creating students. Development data comes from a seeder that the Migrator runs only in the Development environment. Integration tests insert students directly.
- Roster management (create, edit, deactivate, school membership) needs its own ADR when it comes into scope.
