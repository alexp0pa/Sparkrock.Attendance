# Legacy analysis — attendance (3 features)

- **Date:** 2026-10-19
- **Sources (read-only):** `legacy/Database/schema.sql`, `legacy/Database/sp_SaveDailyAttendance.sql`, `legacy/Database/sp_GetStudentAttendance.sql`, `legacy/Forms/frmDailyAttendance.frm`. All 365 lines were read.
- **Scope:** (1) save daily attendance, (2) attendance history per student, (3) chronic absenteeism.
- **Purpose:** to understand the old behaviour. Nothing here is to be ported. The new system is designed from scratch (see `CLAUDE.md`).

The analysis was done by two independent reviews (T-SQL and VB6), which were then cross-checked against the source. The two reviews agreed on every point except the two corrections noted inline in B5 and B1.

## Caveats about the source itself

- The legacy files are incomplete. They reference objects that are not defined anywhere in `legacy/`:
  - `Schools` (sp_SaveDailyAttendance.sql:98)
  - `SchoolTerms` (sp_SaveDailyAttendance.sql:37)
  - `AttendanceSubmissionLog` (sp_SaveDailyAttendance.sql:120)
  - a `SchoolYear()` function (sp_SaveDailyAttendance.sql:83)
  - `sp_GetStudentsForAttendance` (frmDailyAttendance.frm:72)
  - `Nz` (frmDailyAttendance.frm:90)
- `AttendanceCodes` has no seed data (schema.sql:12-19), so the real codes (other than `P`) are unknown.
- The form would not compile or run as written. The control is declared as `VB.MSFlexGrid` (frmDailyAttendance.frm:15), the grid has no cell-editing code, and the connection string points at an `.ini` file (frmDailyAttendance.frm:38).
- So the files describe *intent*, not proven production behaviour. Where it matters, confirm with someone who used the old system.

## Feature 1: Save daily attendance

### Business rules (as implemented)

| # | Rule | Source |
|---|------|--------|
| R1 | The attendance date is always today, taken from the client PC clock. The form cannot record or correct a past day. | frmDailyAttendance.frm:40 |
| R2 | The form loads the whole roster for the school (and grade). A student with no record shows `P` (Present), minutes late `0`, and empty notes. | frmDailyAttendance.frm:72, 90-92 |
| R3 | Every row in the grid is saved, and a blank code becomes `P`. So "present" is the default: untouched students are saved as present. | frmDailyAttendance.frm:106-109 |
| R4 | One call saves the whole class: the rows go as an XML batch to `sp_SaveDailyAttendance(@SchoolID, @AttendDate, @AttendanceXML)`. | frmDailyAttendance.frm:116-117; sp_SaveDailyAttendance.sql:2-4, 24-31 |
| R5 | The save is an upsert keyed on (StudentID, AttendDate): an existing row is updated, otherwise a new one is inserted. Saving again overwrites the earlier values, and no change history is kept. | sp_SaveDailyAttendance.sql:54-77 |
| R6 | Absent and excused are not entered. Both flags come from the code's `AttendanceCodes.IsAbsent` / `IsExcused` and are copied onto the attendance row when it is saved. | sp_SaveDailyAttendance.sql:46-48, 61-62; schema.sql:16-17, 28-29 |
| R7 | Tardiness is just `MinutesLate`, a free-text number turned into a number with `Val`. It is not tied to any code, and tardies are never counted anywhere. | frmDailyAttendance.frm:112; sp_SaveDailyAttendance.sql:63 |
| R8 | A note is optional free text of up to 500 characters (`VARCHAR`, not Unicode). No reason is required for an absence. | schema.sql:31; frmDailyAttendance.frm:113 |
| R9 | The term is looked up from `SchoolTerms` by date. If no term matches, the term stays NULL and the save still succeeds. | sp_SaveDailyAttendance.sql:36-39 |
| R10 | The audit fields `CreatedBy` / `ModifiedBy` are set to `SYSTEM_USER` (the database login), with times from `GETDATE()` (server local time). | sp_SaveDailyAttendance.sql:65-66, 76 |
| R11 | There is no validation: anything goes for unknown or inactive codes, future dates, weekends, negative minutes, students from another school, or inactive students. An unknown code is saved as typed and treated as present. | sp_SaveDailyAttendance.sql:49-53; frmDailyAttendance.frm:101-117 |

### Bugs that matter

| # | Bug | Effect | Source |
|---|-----|--------|--------|
| B1 | **`@ExistingID` is never reset inside the cursor loop.** | Silent data corruption on a re-save. Once a student with an existing row has been processed, a later student *without* a row updates the previous student's row instead of inserting a new one, and the new student gets no record. A first save of the day is unaffected because the variable starts NULL. | sp_SaveDailyAttendance.sql:54-57 |
| B2 | `@IsAbsent` / `@IsExcused` are never reset inside the loop. | An unknown code takes the previous student's absent and excused flags. The `IS NULL` default only works for the first row. | sp_SaveDailyAttendance.sql:46-53 |
| B3 | No transaction, `TRY/CATCH` or `XACT_ABORT`, in either the procedure or the form. | Partial saves. A failure in the middle of the loop leaves earlier students saved, while the form says "Save failed". | sp_SaveDailyAttendance.sql:40-117; frmDailyAttendance.frm:116-121 |
| B4 | No unique constraint on (StudentID, AttendDate), and the code checks for an existing row before inserting without any lock. | Two people saving at the same time can create duplicate days, and those duplicates are counted twice. The lookup also ignores SchoolID. | schema.sql:21-36; sp_SaveDailyAttendance.sql:54-56 |
| B5 | The form builds the SQL and XML by joining strings, without escaping. | SQL injection through the notes field, and an apostrophe breaks the save. `&`, `<` or `"` produce invalid XML, which makes the whole save fail. *Cross-check:* that XML failure is all-or-nothing, because the cast at sp_SaveDailyAttendance.sql:30 runs before any write. | frmDailyAttendance.frm:110-117 |
| B6 | A decimal entry for minutes late (for example `5.5`) cannot be converted to an integer. | One bad cell rejects the whole class. | frmDailyAttendance.frm:112; sp_SaveDailyAttendance.sql:28 |
| B7 | Notes lose their Unicode characters. | Accented and non-Latin text is mangled. | frmDailyAttendance.frm:116 (no `N'`); schema.sql:31 |
| B8 | The grade combo box has no click handler, and the roster loads straight after the grade list is cleared. | The class can't be filtered by grade: the grade is always `''`. | frmDailyAttendance.frm:54-60, 72 |
| B9 | Clicking Save with no school selected still sends school 0 with an empty batch. | A submission row is logged with 0 records. | frmDailyAttendance.frm:101-117; sp_SaveDailyAttendance.sql:120-121 |
| B10 | The date is fixed when the form opens, using the client clock. | A form left open past midnight saves under the wrong day. | frmDailyAttendance.frm:40 |

## Feature 2: Attendance history per student

### Business rules

| # | Rule | Source |
|---|------|--------|
| H1 | `sp_GetStudentAttendance(@StudentID, @SchoolYear = NULL)` takes a school year as `'YYYY-YYYY'`. If none is given, it uses the current school year from the server clock. | sp_GetStudentAttendance.sql:2-16 |
| H2 | It returns one row per day: date, code, code description, absent and excused flags, minutes late, notes and term. Rows are sorted newest first, and days from all schools are included. | sp_GetStudentAttendance.sql:17-35 |
| H3 | It returns a second result set with the stored yearly total of absences, the school's threshold and an `IsChronicallyAbsent` flag. These values are read from the summary table, not recounted from the daily rows. | sp_GetStudentAttendance.sql:36-45 |
| H4 | The school year runs from September 1 to August 31. This boundary is hard-coded in three places. | sp_GetStudentAttendance.sql:10-13, 31-33; sp_SaveDailyAttendance.sql:32-35 |
| H5 | The form has no history screen. This feature exists only as a stored procedure. | frmDailyAttendance.frm (whole file) |

### Bugs that matter

| # | Bug | Effect | Source |
|---|-----|--------|--------|
| B11 | The daily rows are joined to the code table with an `INNER JOIN`, but the save accepts unknown codes. | Days saved with an unknown code silently vanish from history. | sp_GetStudentAttendance.sql:27 vs sp_SaveDailyAttendance.sql:49-53 |
| B12 | The summary result set returns nothing when a student has no summary row yet, or when the `Schools` row is missing. | The caller can't tell "0 absences" from "no data". | sp_GetStudentAttendance.sql:41-45 |
| B13 | The absent and excused flags are a copy made at save time, while the code description is read live. | If a code is redefined later, history shows contradictory values. | sp_GetStudentAttendance.sql:20-22; sp_SaveDailyAttendance.sql:61-62 |
| B14 | The year filter is a `CASE` on `YEAR()` / `MONTH()` of the date column. | No index can be used. A date range (`>= start AND < end`) would be better. | sp_GetStudentAttendance.sql:30-34 |

## Feature 3: Chronic absenteeism

### Business rules

| # | Rule | Source |
|---|------|--------|
| C1 | The measure is a **count of absent days**, not a rate. There is no denominator: no enrolled days and no school days. | sp_SaveDailyAttendance.sql:79-84, 99 |
| C2 | A day counts as absent when `IsAbsent = 1`. **Excused and unexcused absences count the same**, and tardies don't count. | sp_SaveDailyAttendance.sql:84; sp_GetStudentAttendance.sql:40 |
| C3 | A student is chronically absent when the total reaches the school's `Schools.AbsenceAlertThreshold`. The threshold defaults to **10** when it is NULL. | sp_SaveDailyAttendance.sql:98-99; sp_GetStudentAttendance.sql:40 |
| C4 | The total is recalculated on every save and stored in `StudentAttendanceSummary`, which is unique on (StudentID, SchoolYear). | sp_SaveDailyAttendance.sql:78-96; schema.sql:45 |
| C5 | When the threshold is reached, a `CHRONIC_ABSENCE` alert is inserted, at most one *open* alert per student per year. Alerts are never resolved automatically. | sp_SaveDailyAttendance.sql:100-114 |

### Bugs that matter

| # | Bug | Effect | Source |
|---|-----|--------|--------|
| B15 | **The recount is broken.** It calls `SchoolYear(@AttendDate)`, which has no schema prefix and is not defined anywhere. It is also applied to the *parameter*, not to the row's date column. | As written, the procedure probably fails. If it did run, the year filter would always be true, so the count would include absences from **all years**, producing false chronic flags and alerts. | sp_SaveDailyAttendance.sql:83 |
| B16 | The recount filters by SchoolID, but the summary row is keyed and updated without it. | When a student transfers mid-year, the total covers only one school, and the summary row keeps the first school's ID. | sp_SaveDailyAttendance.sql:82, 85-90; schema.sql:45 |
| B17 | When the `Schools` row is missing, the threshold stays NULL. `ISNULL` doesn't apply because the select returns zero rows. | Students are never flagged, and nothing reports it. | sp_SaveDailyAttendance.sql:98-99 |
| B18 | Alerts never close when the count drops below the threshold, for example after a correction. Once someone resolves an alert, the next save of any day re-opens a new one if the count is still at or above the threshold. | Alerts become noisy or stale. | sp_SaveDailyAttendance.sql:101-114 |
| B19 | The stored summary can drift from the daily rows, because of B1–B4, B15 and B16. | The history screen shows a wrong total and a wrong chronic flag. | sp_GetStudentAttendance.sql:36-45 |
