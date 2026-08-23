---
name: mcp-expose
description: Regenerate a module's MCP tool surface from its OpenAPI contract. Use after a contract changes and the module's MCP server needs to reflect it, or when standing up a new module's MCP server.
---

Bring `modules/<module>/src/TPXSoft.<Module>.Mcp` in sync with `contracts/<module>.vN.yaml`.

1. Read `contracts/<module>.vN.yaml` — entities, endpoints, fields.
2. Expose these tools (stdio, `ModelContextProtocol` SDK), per PLAN §0.7:
   - `get_openapi()` — the raw contract
   - `list_endpoints()` / `describe_entity(name)` — cheap structural queries, sourced from the contract, not from `.cs` source
   - `find_consumers(entity_or_field)` — greps `shared/clients/**` and other modules for references to the entity/field
   - `run_tests(filter?)` — shells out to `tpx test <module>` (or `tpx verify <module>`), never to `dotnet test` directly
   - `get_migrations_status()`
3. Confirm the module is registered in root `.mcp.json` (stdio, pointing at the built `Mcp` project).
4. `tpx verify <module>` — must be green.
5. Sanity check from a fresh session: ask a structural question about the module's entity and confirm the answer comes from MCP, not from reading `.cs` source (PLAN verification item 6).
