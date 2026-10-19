# Sparkrock.Attendance: architectural decisions

A from-scratch replacement for the legacy attendance module (VB6 form + T-SQL, kept read-only in `legacy/`). It provides three features: **save daily attendance** for a class, **attendance history** for a student, and **chronic absenteeism status** for a student. It also has a per-school list of chronically absent students.

It's built with .NET 8 Clean Architecture and vertical slices, using EF Core, SQL Server and Aspire. The legacy code was analysed, not ported (`docs/legacy-analysis.md`). Each decision below has a full record in [`docs/adr/`](docs/adr/), which is the source of truth.

## What couldn't be determined and how I handled it

The legacy sources reference objects that aren't included, and parts of them couldn't have run as written. I treated them as a statement of **intent**, not as proven production behaviour.

| Couldn't be determined | How I handled it |
|---|---|
| **The real attendance codes:** `AttendanceCodes` has no data, and only `P` appears (`frmDailyAttendance.frm:90`). | Four fixed statuses (ADR 0003). Migrating legacy data would need a code mapping. |
| **`Schools` and each school's `AbsenceAlertThreshold`:** referenced but never defined. | No school table (ADR 0008). One global threshold, defaulting to the legacy fallback of 10 (ADR 0006). |
| **The school year:** `SchoolYear()` is undefined and applied to the wrong value, and the real start date is unknown. | One school year per count, with a configurable start date that defaults to September 1, implemented once (ADR 0005). |
| **How transfers count:** the legacy count filters by school, but its summary ignores the school. | Counted per student, at the student's school, per school year (ADR 0008). |
| **The roster procedure** (`sp_GetStudentsForAttendance`, missing). | Out of scope. A minimal Student entity plus a dev seeder (ADRs 0009, 0011). |
| **Who entered attendance:** `SYSTEM_USER` is the shared database login. | The logged-in user's JWT email (ADR 0007). |
| **`SchoolTerms`, `AttendanceSubmissionLog`, demographics:** undefined or unused by the three features. | Not modelled. |
| **Which time zone "today" means.** | UTC, flagged for review in ADR 0004. |

## Decisions

| # | Decision | Why |
|---|---|---|
| [0001](docs/adr/0001-database-provider.md) | **SQL Server** | It's the organization standard, and the legacy system already runs on it. |
| [0002](docs/adr/0002-integer-entity-ids.md) | **`int` identity IDs** for new entities | They match the legacy model. The template's auth entities keep their `Guid`. |
| [0003](docs/adr/0003-attendance-statuses.md) | **Fixed statuses:** `Present`, `Late`, `AbsentExcused`, `AbsentUnexcused`. Minutes late only with `Late`. | The legacy code table is empty, its copied flags drifted, and unknown codes were treated as present. |
| [0004](docs/adr/0004-saving-daily-attendance.md) | **One PUT saves a whole class, all or nothing.** One record per student per date. Dates from the current school year up to today. | This makes the legacy partial saves, duplicates and wrong-student overwrites (B1, B3, B4) impossible. |
| [0006](docs/adr/0006-chronic-absenteeism.md) | **Chronic means at least N absent days in the school year** (N is configurable, default 10). Excused absences count; late arrivals don't. **Computed on every request** by one shared calculation used by both endpoints. | This keeps the legacy rule without its stored summary, which drifted and counted every year (B15, B19). |
| [0007](docs/adr/0007-audit-columns.md) | **Audit columns hold the JWT email**, with UTC timestamps. | Legacy recorded the database login, not the teacher. |
| [0009](docs/adr/0009-student-entity.md), [0011](docs/adr/0011-student-school-and-seed-command.md) | **A minimal Student entity** with `SchoolId`, plus an Aspire **Seed students** command | Records can't point at students who don't exist, and testers need students. |
| [0010](docs/adr/0010-enums-as-strings-in-api.md) | **Statuses are stored and sent as names** | The API describes itself and the stored data is readable. |

**Enforced in the code:**
- A save is a single `SaveChangesAsync`, so it runs as one transaction.
- The database backs up the validation with a unique (StudentId, Date) index, a check constraint and a restricted foreign key.
- Settings are validated at startup.
- Integration tests run on a fixed clock against real SQL Server (Testcontainers).

## Known gaps

- **Authorization:** any logged-in user can read and write any school or student.
- **No delete:** attendance records can be overwritten, not removed.
- **No roster management.**

## Run and test

```bash
dotnet run --project src/AppHost --launch-profile http   # Aspire dashboard; Swagger at http://localhost:5000/swagger
dotnet test                                               # Docker required for integration tests
```

Generated with `dotnet new ca-simplified-api` from my personal .NET 8 Clean Architecture template, which is derived from Milan Jovanović's public template.
