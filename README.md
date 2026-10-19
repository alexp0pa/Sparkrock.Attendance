# Sparkrock.Attendance: architectural decisions

A from-scratch replacement for the legacy attendance system (VB6 form + T-SQL, read-only in `legacy/`), covering three features: **save daily attendance**, **attendance history per student** and a **chronic absenteeism list per student**. After the recording I noticed the third feature was built as a per-school list rather than a per-student status, as the brief asks. I added GET students/{id}/chronic-absenteeism in the same session, reusing the same calculation; it's in its own commit.

The architecture is .NET 8 Clean Architecture with vertical slices, EF Core, SQL Server and Aspire. The legacy code was analysed, not ported (`docs/legacy-analysis.md`). Every decision below has a full record in [`docs/adr/`](docs/adr/), which is the source of truth.

## What couldn't be determined and how we handled it

The legacy sources are incomplete. They reference objects that don't exist in `legacy/`, and parts of them could not have compiled or run as written. We treated them as a statement of **intent**, not proven production behaviour (`docs/legacy-analysis.md`).

| Couldn't be determined | How we handled it |
|---|---|
| **The real attendance codes.** `AttendanceCodes` has no seed data; only `P` appears (`frmDailyAttendance.frm:90`). | Four fixed statuses (ADR 0003). Migrating legacy data would need a code mapping. |
| **The `Schools` table and each school's `AbsenceAlertThreshold`** (referenced, never defined) | One global threshold, defaulting to legacy's fallback of 10 (ADR 0006). No school table (ADR 0008). |
| **The `SchoolYear()` function** (`sp_SaveDailyAttendance.sql:83`): undefined, and applied to the parameter rather than the row, so it couldn't work | Implemented the evident intent, one school year per count, in a single place (ADR 0005). We didn't reproduce the all-years count (B15). |
| **Whether September 1 is the real school-year start for every school** | A configurable start date, defaulting to September 1 (ADR 0005). |
| **What the roster procedure returned** (`sp_GetStudentsForAttendance`, missing) | Roster loading is out of scope. Students come from a minimal entity plus a seeder (ADR 0009, 0011). |
| **How student transfers were meant to count.** Legacy's absence count filters by school, but its stored summary ignores the school. | Kept the legacy count rule: per student, per school, per school year (ADR 0008). |
| **Who actually entered attendance.** `SYSTEM_USER` is the shared database login. | The logged-in user's JWT email (ADR 0007). |
| **`SchoolTerms` and `AttendanceSubmissionLog`** (referenced, never defined) | Not modelled; out of scope. |
| **Whether the form ever ran as written** (no grid editing, `Nz` undefined, broken connection string) | Used it only for intended behaviour, such as the grid defaulting students to `P` and the whole class being saved in one call. The new API requires an explicit status instead (ADR 0004). |
| **Which time zone "today" means** | UTC, flagged in ADR 0004 for review if schools span time zones. |

## Platform and model

| # | Decision | Why |
|---|---|---|
| [0002](docs/adr/0002-integer-entity-ids.md) | **`int` identity IDs** for new entities. `User`/`RefreshToken` keep the template's `Guid`. | The legacy model uses ints. The template's auth code stays untouched. |
| [0009](docs/adr/0009-student-entity.md) | A minimal **Student** entity. Attendance records have a foreign key to it. | Attendance can't be recorded for students who don't exist; legacy allowed that. |
| [0011](docs/adr/0011-student-school-and-seed-command.md) | `Student.SchoolId` plus an Aspire **Seed students** command | Testers need students in a given school. There is no roster API. |
| [0008](docs/adr/0008-school-scoping.md) | **No school table.** `SchoolId` is a plain int stored on each record, as in legacy. | No school entity is available. |

## Attendance rules

| # | Decision | Why |
|---|---|---|
| [0003](docs/adr/0003-attendance-statuses.md) | **Four fixed statuses:** `Present`, `Late`, `AbsentExcused`, `AbsentUnexcused`. Minutes late only with `Late`. No code table. | Legacy's code table is empty. Its copied flags drifted, and unknown codes were silently treated as present. |
| [0004](docs/adr/0004-saving-daily-attendance.md) | **One PUT saves a whole class, all or nothing.** One record per student per date (unique index). Every student needs an explicit status. Dates from the current school year up to today. | This makes legacy's partial saves, duplicates and wrong-student overwrites (B1, B3, B4) impossible. Past days can be corrected. |
| [0005](docs/adr/0005-school-year.md) | **One configurable school-year start** (default September 1), implemented once. | Legacy hardcoded the start date in three places. |
| [0006](docs/adr/0006-chronic-absenteeism.md) | **Chronic means at least N absent days** in the school year. N is a global setting, default 10. Excused absences count; late arrivals don't. **Computed on every request.** Two endpoints share one calculation: `GET students/{id}/chronic-absenteeism` returns one student's status at their own school, and `GET schools/{id}/chronic-absenteeism` lists a school's chronically absent students. | This keeps the legacy rule, minus its stored summary, which drifted and counted every year (B15, B19). |
| [0007](docs/adr/0007-audit-columns.md) | **Audit columns hold the JWT email** of the logged-in user, with UTC timestamps. | Legacy recorded the shared database login (`SYSTEM_USER`), not the teacher. |
| [0010](docs/adr/0010-enums-as-strings-in-api.md) | **Statuses are names** in JSON and in the database. | The API describes itself and the stored data is readable. |

## Implementation choices (within the ADRs)

- **Atomic save:** a single `SaveChangesAsync` per save, which runs as one transaction. Only a unique-index race returns **409**, which is safe to retry; other database errors are real failures.
- **Database guards:** the database also enforces the rules where it can, with a unique (StudentId, Date) index, a check constraint on minutes late, and a delete-restricted foreign key to Student.
- **Fail-fast settings:** `AttendanceOptions` (threshold, school-year start) are validated at startup.
- **Dev-only seed data:** demo students come only from the Migrator in Development, or from the dashboard command. No `HasData`, so production gets no demo data.
- **Deterministic tests:** integration tests run on a **fixed clock** (2026-03-16) against real SQL Server (Testcontainers). Each ADR has a test that proves it.

## Known gaps (not yet decided)

- **Authorization:** any logged-in user can read and write any school or student.
- **No delete:** an attendance record can only be overwritten, not removed.
- **No roster management.**
- **Not modelled from legacy:** `TermID` and the student demographic columns.

## Run and test

```bash
dotnet run --project src/AppHost --launch-profile http   # Aspire dashboard + Swagger at http://localhost:5000/swagger
dotnet test                                               # Docker required for integration tests
```

Generated with `dotnet new ca-simplified-api` from a personal .NET 8 Clean Architecture template, which is derived from Milan Jovanović's public Clean Architecture template.
