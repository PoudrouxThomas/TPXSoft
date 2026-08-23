---
name: tpx-goal
description: Reads and updates GOALS.md against real repo state, and reports milestone progress. Use when asked to check goal status, update GOALS.md, or summarize what's done/blocked in the current phase.
---

Keep `GOALS.md` honest against the actual repo — never against notes, intent, or what a previous run claimed.

1. Read `GOALS.md` and the matching section of `PLAN.md` for the milestone(s) in scope.
2. For each checkbox, verify against real state, not memory:
   - File/dir existence → `Glob`/`Read`.
   - Command behavior (`tpx verify <module>`, `tpx contract lint`, `tpx verify boundaries`, etc.) → actually run it, don't assume last session's result still holds.
   - Agent/skill/hook presence → confirm the file exists under `.claude/agents`, `.claude/skills`, or is wired in `.claude/settings.json`.
3. Flip `[ ]`/`[x]` only where evidence disagrees with the current state. Never re-check a box just because it was checked before — re-verify it.
4. When an item is blocked, keep the note naming the specific blocker (missing tool, sandbox limitation, unmet dependency) rather than a generic "not done" — that's what lets the next run skip re-diagnosing it.
5. Report a short progress summary: what flipped, what's still blocked and why, what's the next actionable item.

Do not invent new milestones or restructure `GOALS.md`'s sections — this skill updates checkbox state and notes in place, matching `PLAN.md`'s phase/section structure. If `PLAN.md` itself changed (new phase, changed acceptance criteria), flag that to the user instead of silently reshaping `GOALS.md` around it.
