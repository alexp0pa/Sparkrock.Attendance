# 0010. Enums as strings in the API

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

By default the template exchanges enums as integers in JSON, so clients have to know the number behind each attendance status (ADR 0003).

## Decision

- **Responses:** enums are serialized as their names (`"Present"`, `"Late"`, `"AbsentExcused"`, `"AbsentUnexcused"`), using a global `JsonStringEnumConverter` in Web.Api.
- **Requests:** status values arrive as strings and are checked by the command validator (case-insensitive) before the handler parses them. An invalid value therefore returns a normal 400 validation error, instead of a model-binding failure that would surface as a 500.
- **Database:** the status is also stored as its name, using a string conversion with `HasMaxLength(20)`.

## Alternatives considered

- **Integers, the template default.** Rejected because the API isn't self-describing and the stored data is opaque.

## Consequences

- Renaming an enum member is a breaking change for both the API and the stored data.
- Clients see readable values in Swagger and in responses.
