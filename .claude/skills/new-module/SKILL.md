---
name: new-module
description: Scaffold a new TPXSoft module — full src/tests tree, its own .sln, CI job, CLAUDE.md, empty contract, MCP server — and register it in .mcp.json and CODEOWNERS. Use when asked to create a new module (e.g. "new-module documents").
---

Scaffold module `<name>` per the target layout in root `CLAUDE.md` and PLAN.md.

1. If another module already exists, copy its shape as the template (dir names, `.sln` structure, `CLAUDE.md` sections). Otherwise follow the layout in root `CLAUDE.md` literally.
2. Create `modules/<name>/`:
   - `src/TPXSoft.<Name>.Domain/`, `src/TPXSoft.<Name>.Api/`, `src/TPXSoft.<Name>.Infrastructure/`, `src/TPXSoft.<Name>.Mcp/` (stdio `ModelContextProtocol` server — see `mcp-expose` skill for its tool surface)
   - `tests/TPXSoft.<Name>.UnitTests/`, `tests/TPXSoft.<Name>.IntegrationTests/`
   - `TPXSoft.<Name>.sln` (module-scoped, for fast agent builds)
3. Add root-solution reference: include the new `.sln`'s projects in `TPXSoft.sln`.
4. Create `contracts/<name>.v1.yaml` — minimal valid OpenAPI stub (info + empty paths). Real endpoints come later via `new-endpoint`.
5. Add `.github/workflows/<name>.yml`, path-filtered to `modules/<name>/**`.
6. Register the module's MCP server in root `.mcp.json` (stdio, pointing at the built `Mcp` project).
7. Add `modules/<name>/` to `CODEOWNERS`.
8. Write `modules/<name>/CLAUDE.md`: bounded context, entities, its `tpx verify <name>` line, known consumers (PLAN §0.10). Loaded only when working in that module.
9. Run `tpx verify <name>` — must resolve cleanly (empty module is fine; build/sln wiring must not error).

Never hand-write anything under `shared/clients/**` — that stays generated, even for a brand-new module's client.
