# Squad Team

> woodgrove-groceries

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Morpheus | Lead / Architect | .squad/agents/morpheus/charter.md | 🏗️ Lead |
| Trinity | Identity / Auth Specialist | .squad/agents/trinity/charter.md | 🔒 Auth |
| Tank | Backend / API Dev | .squad/agents/tank/charter.md | 🔧 Backend |
| Dozer | Cloud / DevOps | .squad/agents/dozer/charter.md | ⚙️ DevOps |
| Switch | Frontend Dev | .squad/agents/switch/charter.md | ⚛️ Frontend |
| Scribe | Session Logger | .squad/agents/scribe/charter.md | 📋 Scribe |
| Ralph | Work Monitor | .squad/agents/ralph/charter.md | 🔄 Monitor |
| Rai | RAI Reviewer | .squad/agents/Rai/charter.md | 🛡️ RAI |
| Fact Checker | Verification & Devil's Advocate | .squad/agents/fact-checker/charter.md | 🔍 Verifier |


## Coding Agent

<!-- copilot-auto-assign: false -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Coding Agent |

### Capabilities

**🟢 Good fit — auto-route when enabled:**
- Bug fixes with clear reproduction steps
- Test coverage (adding missing tests, fixing flaky tests)
- Lint/format fixes and code style cleanup
- Dependency updates and version bumps
- Small isolated features with clear specs
- Boilerplate/scaffolding generation
- Documentation fixes and README updates

**🟡 Needs review — route to @copilot but flag for squad member PR review:**
- Medium features with clear specs and acceptance criteria
- Refactoring with existing test coverage
- API endpoint additions following established patterns
- Migration scripts with well-defined schemas

**🔴 Not suitable — route to squad member instead:**
- Architecture decisions and system design
- Multi-system integration requiring coordination
- Ambiguous requirements needing clarification
- Security-critical changes (auth, encryption, access control)
- Performance-critical paths requiring benchmarking
- Changes requiring cross-team discussion

## Project Context

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application
- **Lead contact:** David Hart
- **Created:** 2026-07-21
- **Universe:** The Matrix
- **Stack:** ASP.NET Core Razor Pages (C#), Microsoft Entra External ID (CIAM), Microsoft Graph, Azure
- **Related components:**
  - `woodgrove-groceries-api` — Woodgrove Groceries web API
  - `woodgrove-groceries-graph-middleware` — middleware for Microsoft Graph
  - `woodgrove-auth-api` — custom authentication extension web API for Entra External ID
- **Active initiatives:** repo audit / modernization, monorepo consolidation evaluation, Bicep-based Azure deployment
