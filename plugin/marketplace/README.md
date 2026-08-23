# tpxsoft-marketplace (local, not published)

Local-only marketplace bundling the `tpxsoft` plugin (agents + skills + hooks
from `.claude/`, PLAN.md §0.9). Not pushed anywhere remote — demo project,
kept simple.

`plugin/marketplace/plugins/tpxsoft/` is a generated bundle. Source of truth
stays `.claude/agents`, `.claude/skills`, `.claude/settings.json`,
`.claude/hooks/session-start.sh` — re-copy by hand into the plugin dir when
those change.

## Try it locally

```bash
claude plugin marketplace add ./plugin/marketplace
claude plugin install tpxsoft@tpxsoft-marketplace
claude plugin list
```

Remove again:

```bash
claude plugin uninstall tpxsoft@tpxsoft-marketplace
claude plugin marketplace remove tpxsoft-marketplace
```

## Validate

```bash
claude plugin validate plugin/marketplace/plugins/tpxsoft
```

`claude plugin eval` is early-access and gated on this account
(`` `plugin eval` is currently in early access ``, exit 1) — `validate` above
is the working gate for this pass.
