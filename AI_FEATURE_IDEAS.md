# AI Feature Ideas for Netrom Eco Meal

A separate brainstorm from [FEATURE_IDEAS.md](FEATURE_IDEAS.md) — split out because these all
share one dependency (a local LLM) and one architecture, rather than being independent phase
work. Nothing here is committed to; see FEATURE_IDEAS.md for where this fits relative to
everything else non-AI (Phase 8 business analytics is a soft dependency for the last item
below, otherwise this list doesn't block or get blocked by anything in that file).

> **Status (2026-08-08):** New this round, unshipped. Modeled loosely on the sibling **Smart
> Shopping Assistant** project's agent-based shape (natural-language search, a budget planner,
> tool-calling agents that only ever surface real data), but Eco Meal has no paid-API account to
> lean on — everything here runs on a free, self-hosted **Ollama** model in Docker, with no other
> AI provider or hosted API in the loop anywhere in the stack.

## Architecture

`IChatClient` (the `Microsoft.Extensions.AI` abstraction) is backed by **`OllamaSharp`**, which
implements `IChatClient` natively against Ollama's own API — no OpenAI-compatible shim, no
`OpenAI` NuGet package, no third-party hosted endpoint of any kind. Ollama itself runs as its
own service in `docker-compose.test.yml` (image `ollama/ollama`, a volume so pulled models
survive a container restart, same "runs locally with zero real-account setup" spirit as the
Mailpit addition in Phase 5 of FEATURE_IDEAS.md). Registration in `Program.cs` reads
`Ollama:BaseUrl`/`Ollama:ModelId` from config, same "empty/unreachable → friendly 'AI features
aren't available yet' error" convention as `Stripe:SecretKey` — except there's no real key to
ever configure, paid or otherwise.

Tool-calling and JSON-schema output (`ChatOptions.ResponseFormat = ChatResponseFormat
.ForJsonSchema<T>()`, `Temperature = 0` to keep tool use deterministic, agent tools built with
`AIFunctionFactory` over `PackageService`/`OrderService` methods) work the same way through
`OllamaSharp`'s `IChatClient` as they would through any other implementation — that part of the
sibling project's pattern carries over unchanged. What doesn't carry over is any assumption
that the underlying model behaves like a hosted frontier model: **not every free local model
reliably does tool-calling *and* strict JSON-schema output together.** The two items below that
call tools (budget planner, markdown/pricing assistant) need a model Ollama has verified
function-calling support for — `qwen2.5:7b` or `llama3.1:8b` are the current safe picks, sized
to still run acceptably on a laptop GPU or even CPU. The two text/JSON-only items (descriptions,
search-filter extraction) can use the same model or a smaller/faster one, since they never call
a tool. If a picked model turns out not to hold up on tool-calling or schema adherence in
practice, the fix is trying a different Ollama model, not reaching for a hosted API.

## Sequencing

Cheapest/most self-contained first, since the first item is also what stands up the shared
`IChatClient`-over-Ollama wiring everything else here reuses — and it's a good place to first
confirm the local model's output quality is actually good enough before investing in the
tool-calling items.

- [ ] **AI-assisted package descriptions**: a "Write it for me" button next to
  `PackageForm.razor`'s Description field — manager fills in name/type/dietary tags, one
  `IChatClient.GetResponseAsync` call drafts a short customer-facing description, manager can
  edit before saving. No tool-calling, no JSON schema, no new data model — the cheapest possible
  way to stand up the Ollama plumbing this whole list depends on, and removes real daily
  friction (recurring templates already assume a package gets described once and reused, but a
  fresh one-off package still needs its description typed from scratch every time today).
- [ ] **Conversational natural-language search**: layer an AI intent parser in front of
  `Home.razor`'s existing search/filter/sort — "vegan dinner under 30 lei near me closing
  soon" gets extracted into structured filters (dietary tags, price ceiling, distance,
  closing-soon sort) and applied against the real package list the same way the current
  literal substring search does, exactly like the sibling project's `AiSearchService`: the LLM
  only ever produces a schema-constrained filter object, the actual matching stays
  deterministic C#, so a bad filter can narrow results but can never fabricate one. Multi-turn
  refinement ("cheaper", "gluten-free only") reuses the same turn-history pattern. No tool
  calls needed — just needs the local model to hold `ForJsonSchema` structured output
  reliably, worth a quick manual sanity check with a handful of real queries before trusting it.
  Depends on the Ollama wiring from the item above.
- [ ] **Budget/goal rescue-basket planner**: "Feed 4 people, vegetarian, under 60 lei" plus a
  budget produces a proposed basket of real, in-stock, live packages — an agent tool calls into
  `PackageService`/`PackageController` (mirroring `BudgetPlannerAgent`'s
  `SearchProductsByCategory` tool) to fetch actual candidates, then composes a basket that fits
  the budget with a reason per item, shown for per-item approve/decline before anything touches
  `CartService`. Needs one Eco Meal-specific rule the sibling project never had:
  `CartService`'s single-business-per-basket constraint, so the agent has to either compose
  within one kitchen or explain when the ideal picks span kitchens it can't combine. First item
  here that actually needs Ollama's tool-calling to hold up under real use — pick
  `qwen2.5:7b`/`llama3.1:8b` (or newer verified-tool-calling model) here, and keep
  `Temperature = 0` and the "never invent a package/price" instruction from the sibling
  project's prompt, since a free local model is more prone to drifting off the tool result than
  a hosted frontier model would be — worth extra manual spot-checking here specifically.
- [ ] **Near-expiry impact nudges**: extends the existing `Notification`/`Favorite`/back-in-
  stock infrastructure (Phase 2 and 5 of FEATURE_IDEAS.md) with an AI-scored trigger instead of
  a hand-coded rule — a periodic sweep (same shape as `OrderLifecycleSweepService`) flags
  packages closing soon and still unclaimed at kitchens a customer favorites or has ordered
  from before, and drafts the nudge copy ("2 portions left at {kitchen}, closing in 20 minutes
  — matches your usual vegan order"). Reuses the bell + email channels as-is; the only new
  piece is smarter targeting than "every favorite gets every alert." Pure text generation, no
  tools — the lighter model from the first two items is enough. Depends on the Ollama wiring
  from the first item.
- [ ] **Manager markdown/pricing assistant**: builds on FEATURE_IDEAS.md's Phase 8 business
  analytics (sell-through rate) — as a package's `PickupEnd` approaches with stock still
  unsold, a tool-calling agent looks at that package's (and similar past packages') actual
  historical sell-through via `OrderService`/`PackageService` queries and suggests a specific
  price cut ("Cut to 12 lei — similar boxes here sell out in the last hour at that price"),
  shown as a dismissable suggestion on `/packages`, never applied automatically. Highest value
  here — directly reduces food waste, the app's core mission — but also the riskiest to get
  wrong (a bad suggestion costs a manager real revenue, and a local model is the least-trusted
  link in the chain), so ship it last, on the tool-calling model already proven out by the
  budget planner, and only once that item's real-world accuracy has actually been checked.
