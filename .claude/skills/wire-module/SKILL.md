---
name: wire-module
description: Connect module A to module B through B's generated client — DI registration plus a contract test. Use when asked to make one module call another.
---

Wire module `<A>` to call module `<B>`, staying inside the boundary rules `tpx verify boundaries` enforces.

1. Confirm the endpoint A needs already exists in `contracts/<B>.vN.yaml`. If not, that's a `new-endpoint` task on B first — do that, then come back here.
2. `tpx gen` if B's contract changed since the last generation, so `shared/clients/csharp` has a current client for B.
3. In A's `Infrastructure` (or `Api`) layer, register B's generated client in DI. Never reference `TPXSoft.<B>.Domain` or `TPXSoft.<B>.Infrastructure` directly — only `shared/clients/*` or `shared/TPXSoft.Shared.Kernel`.
4. Add a contract test in A's tests asserting the call against B's client matches the contract shape. Stub/mock at the HTTP boundary — never against B's internals.
5. `tpx verify <A>` and `tpx verify boundaries` — both must be green before reporting done.
