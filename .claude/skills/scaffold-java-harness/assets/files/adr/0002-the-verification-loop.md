# 2. One verification loop, called through ./dev

Date: __DATE__

## Status

Accepted

## Context

An agent that can check its own work is worth several times one that cannot. If the loop is
slow, agents stop verifying and start guessing, and a harness that looks like it works is worse
than none. If the loop is noisy, its output lands in the model context on every run and becomes
the largest silent token leak in the repository.

## Decision

`./dev verify` is the only verification entry point: format, lint, compile with warnings as
errors, unit and architecture tests. It stops at the first failure, prints one line when green,
and prints only the failing check when red. Hooks, CI, agents and humans all call it.

Targets: under 60 seconds, under 15 lines of output on success. Integration tests are tagged
`it` and stay outside it.

## Consequences

The build tool can be swapped without touching hooks, CI or documentation. The cost is one
indirection, and the discipline of never adding a check to CI that verify does not run.
