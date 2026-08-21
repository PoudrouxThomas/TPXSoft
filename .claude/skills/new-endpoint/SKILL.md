---
name: new-endpoint
description: Add a new API endpoint the contract-first way — edit the OpenAPI contract, regenerate clients, implement the handler, write a test, verify. Use when asked to add or change an endpoint on an existing module.
---

Contract-first loop for one endpoint on module `<module>`. Never write the handler before the contract.

1. Edit `contracts/<module>.vN.yaml` — add/change the path and schema.
2. `tpx contract lint` — confirms the YAML is valid and not a breaking change vs `main`. Fix before continuing.
3. `tpx gen` — regenerates `shared/clients/csharp` and `shared/clients/angular` from the contract. Never hand-edit anything under `shared/clients/**` or `**/generated/**`; if generated output looks wrong, fix the contract and regenerate.
4. Implement the handler in `modules/<module>/src/TPXSoft.<Module>.Api` (plus `Domain`/`Infrastructure` as needed). Stay inside this module — reach other modules only via their generated client or `Shared.Kernel`.
5. Write a test: unit test in `tests/TPXSoft.<Module>.UnitTests`, or an integration test if it touches Postgres. Delegate to `test-writer` if the acceptance criteria are already spelled out.
6. `tpx verify <module>` — must be green before reporting done.
