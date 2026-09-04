# 1. Record architecture decisions

Date: __DATE__

## Status

Accepted

## Context

Prose specifications rot silently and agents believe them. A decision that lives only in a
chat thread or a wiki page stops being true the moment the code moves, and nothing fails.

## Decision

Decisions that shape the code are recorded here as short, append-only ADRs. Existing ADRs are
never edited; a decision that changes gets a new ADR that supersedes the old one.

Anything that can be executable is executable instead: the API surface is the OpenAPI document,
behaviour is test names, architecture is ArchUnit rules. ADRs carry only what none of those can.

## Consequences

The history of why stays readable. The cost is one small file per real decision, which is the
cheapest documentation in the repository.
