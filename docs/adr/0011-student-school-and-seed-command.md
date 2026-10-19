# 0011. Student school and a "Seed students" command

- **Status:** Accepted (amends ADR 0009)
- **Date:** 2026-10-19

## Context

Human testers need students in a given school, and there is no roster API (ADR 0009). They want an Aspire dashboard command that asks for a school ID and a number of students, and seeds them with the same `DevelopmentDataSeeder` the Migrator uses.

ADR 0009 gave `Student` no school, so a school ID had nowhere to be stored. Legacy students do belong to a school (`legacy/Database/schema.sql:3`).

## Decision

- **Student school:** `Student` gets a `SchoolId`, a plain `int` with no foreign key and an index, because there is no school table (ADR 0008). This replaces the "no `SchoolId`" part of ADR 0009.
- **Attendance unchanged:** attendance records keep their own `SchoolId`, which comes from the save request (ADR 0008). History and the chronic list are unchanged and still use the record's school.
- **Command:** the AppHost adds a **Seed students** command to the `Database` resource. It prompts for the school ID (1 or more) and the number of students (1–500), then calls `DevelopmentDataSeeder.AddStudentsAsync`. The new student IDs are written to the database resource's console log.
- **Same seeder:** the Migrator's Development seed uses the same method, adding 10 students to school 1 when the table is empty.

## Alternatives considered

- **Seed an attendance record per student at the school**, with no model change. Rejected: testers would start with records they didn't enter.
- **Drop the school prompt.** Rejected: testers want students grouped by school.

## Consequences

- Students that existed before the migration get `SchoolId = 0`, the column default; in practice that is only local development data.
- Nothing checks that attendance is saved for the student's own school. A record's `SchoolId` can differ from the student's (ADR 0008).
- The AppHost references Infrastructure as a library (`IsAspireProjectResource="false"`) so it can run the seeder in-process. It suppresses `ASPIREINTERACTION001`, because the prompt-validation API is experimental in Aspire 9.5.
- The command exists only in local orchestration and never in a deployment.
