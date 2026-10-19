# Clean Architecture Agent Skills for Claude Code

A skill pack that teaches Claude Code the conventions of this .NET 8 Clean Architecture solution — so every feature it builds looks hand-written: vertical-slice use cases, custom command/query handlers (no MediatR), Result-based error handling, minimal API endpoints, and full test coverage.

| Skill | Invoke with | What it does |
|---|---|---|
| **add-entity** | `/add-entity Project with a name and owner` | Adds a domain entity end to end: entity, error catalog, EF configuration (SQL Server), DbContext wiring, then a migration. |
| **add-migration** | `/add-migration Add_Projects` | Creates an EF Core migration with the pinned local `dotnet-ef` tool (Infrastructure project, Web.Api as startup). |
| **add-feature** | `/add-feature archive a project` | Scaffolds a complete vertical slice: command/query, handler, validator, endpoint, and unit + validator + integration tests. |
| **add-tests** | `/add-tests ArchiveProjectCommand` | Backfills handler, validator, and integration tests for existing use cases. |
| **ca-review** | `/ca-review` | Reviews pending changes against the conventions in a forked context: layer boundaries, error handling, security, data, and test coverage. |

Claude picks the right skill automatically when you describe the task ("add an endpoint to archive a project"); invoking them explicitly also works.

Each skill is a plain Markdown file (`SKILL.md`, plus templates under `references/`). Edit the templates once and every future feature follows suit.

Related project automation lives in `.claude/settings.json` (permissions and hooks) and `.claude/hooks/`.
