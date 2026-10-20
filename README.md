# AppLifePlanner

[![CI](https://github.com/johnnysb160/AppLifePlanner-Showcase/actions/workflows/ci.yml/badge.svg)](https://github.com/johnnysb160/AppLifePlanner-Showcase/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Blazor](https://img.shields.io/badge/Blazor-Interactive%20Server-5C2D91)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-EF%20Core-336791)

AppLifePlanner is a personal operating system for finances, investments, routines, annual planning, and daily execution. It turns financial and planning data into a focused answer to three questions: **what matters now, what this week serves, and whether the year is moving toward its goals**.

> This repository is a sanitized public snapshot of a private production project. It contains no production database, credentials, personal transactions, or private operational history. The public demo uses a read-only synthetic tenant.

## Product snapshot

![AppLifePlanner dashboard with synthetic data](docs/images/dashboard.png)

## Why this project exists

Most personal-finance applications stop at reporting, while planning tools require the user to manually repeat information that already exists elsewhere. AppLifePlanner connects both sides: canonical financial facts drive annual projections, weekly focus, and small actionable recommendations without creating a second ledger or requiring a paid AI service.

## End-to-end product flow

1. **Capture and reconcile reality.** Accounts, cards, transactions, allocations, statement imports, and e-mail-originated entries converge on one owner-scoped financial model. Deterministic matching prevents duplicate ledger entries and sends ambiguous records to review.
2. **Price and classify the position.** Market providers, currency conversion, allocation history, and fixed-income formulas calculate the current position while preserving the last known good quote when an external provider fails.
3. **Turn facts into a plan.** Net worth, reserves, contributions, operating cash flow, and allocation performance feed annual projections and financial key results without asking the user to type the same numbers again.
4. **Connect the year to this week.** Annual plans, pillars, OKRs, quarterly tasks, risks, habits, reviews, and a three-item weekly focus create a traceable line from long-term intent to current commitments.
5. **Make today executable.** The Today experience combines the current routine block, next events, financial pulse, weekly focus, and deterministic action cards that explain the evidence behind each suggested next step.
6. **Extend without duplicating the engine.** Windows and iOS/Scriptable companions consume narrow, paired Today and routine APIs; they never connect directly to the database or implement a second planning model.

## Product depth

| Area | Capabilities |
|---|---|
| Personal finance | Accounts, credit cards, transactions, categories, balances, allocations, invested-capital history, soft deletion, and financial privacy masking |
| Imports and reconciliation | Bank CSV/OFX, Nubank invoice CSV semantics, IMAP ingestion, deterministic identity, duplicate detection, review queue, and one-to-one reconciliation |
| Investments | B3, US, crypto, FX, and fixed-income pricing with provider fallback, CDI/SELIC/IPCA calculations, currency conversion, and quote history |
| Planning | Annual plans, pillars, objectives, key results, quarterly tasks, weekly focus, compact and advanced reviews, habits, risks, and snapshots |
| Daily execution | Time-based routine, action center, explainable local recommendations, direct links to the smallest next action, and current-week context |
| Life administration | Vehicle records, service history, odometer and cost summaries, due/overdue reminders, and oil-change follow-up |
| Companion surfaces | WPF Windows overlay, revocable widget pairing, routine lock-screen snapshots, Scriptable iOS runtime, and native WidgetKit source |
| Operations | Admin system-status case study, database and provider health, safe structured logging, access analytics, Docker deployment, and automated PostgreSQL backup workflow |

## Highlights

- **Today dashboard** combining current routine, timeline, weekly focus, financial position, and deterministic action cards.
- **Financial core** for accounts, transactions, credit cards, allocations, fixed-income valuation, statements, and reconciliation.
- **Integrated planning** with annual plans, OKRs, quarter plans, routines, compact reviews, habits, risks, and progress snapshots.
- **Canonical financial facts** shared by dashboards and planning, preventing competing totals and formulas.
- **Read-only public demo** protected at the persistence boundary, not only in the UI.
- **Companion experiences** for Windows and iOS/Scriptable using paired, revocable device tokens.
- **Privacy and tenancy** through ASP.NET Core Identity, owner-scoped data, encryption, rate limits, and safe logging.
- **Resilient integrations** with local fallbacks for market data, email ingestion, and optional AI-provider connectivity checks.

## Rules that keep the product coherent

- Planning reads from a canonical financial-facts layer; pages cannot invent their own net-worth or projection formulas.
- Reliable values are derived before the user is asked for input, keeping plans synchronized with real financial activity.
- Recommendations are deterministic, local, and explainable; paid AI is not required for the core experience.
- The anonymous demo is a real synthetic tenant, and write attempts are rejected in the persistence layer rather than hidden only by disabled buttons.
- Background work preserves tenant ownership and failure isolation across market data, e-mail sync, planning actions, analytics, and health checks.

## Architecture

```mermaid
flowchart LR
  Browser[Blazor Interactive Server] --> Web[ASP.NET Core Web Host]
  Windows[Windows Widget] --> API[Paired Widget API]
  IOS[iOS / Scriptable Widgets] --> API
  API --> Web
  Web --> Domain[Domain Services]
  Domain --> EF[EF Core + Tenant Filters]
  EF --> DB[(PostgreSQL)]
  Web --> Providers[Market / SMTP / IMAP Providers]
  Providers --> Web
```

The Web host owns the application pipeline, APIs, domain services, background jobs, and market-data updates. Shared contracts live in a separate project; the Windows widget consumes the same paired API instead of connecting to the database directly.

[Read the architecture case study](docs/architecture.md)

## Technology

- .NET 8, ASP.NET Core, Blazor Interactive Server
- Entity Framework Core and PostgreSQL
- ASP.NET Core Identity, JWT, Data Protection, AES-256-GCM
- xUnit contract and service tests
- WPF Windows companion
- SwiftUI/WidgetKit source and Scriptable runtime for iOS
- Docker and GitHub Actions

## Repository structure

```text
AppControlOrganizer.V2.Web/           Web UI, APIs, services, EF Core, integrations
AppControlOrganizer.V2.Shared/        Entities, DTOs, enums, shared contracts
AppControlOrganizer.V2.Tests/         Contract and behavior tests
AppControlOrganizer.V2.WindowsWidget/ Windows companion
AppLifePlanner.iOS/                   iOS and Scriptable widget sources
docs/                                 Public architecture and privacy notes
```

## Run locally

Prerequisites: .NET 8 SDK and PostgreSQL.

Provide development values through environment variables or user secrets. Required runtime foundations are the PostgreSQL connection, a 32-byte Base64 encryption key, and JWT signing configuration. Provider integrations are optional.

```powershell
dotnet restore AppControlOrganizer.V2.sln
dotnet run --project AppControlOrganizer.V2.Web
```

The application applies pending migrations at startup. Never place real credentials in tracked configuration files.

## Verification

```powershell
dotnet build AppControlOrganizer.V2.sln --nologo
dotnet test AppControlOrganizer.V2.sln --no-build --nologo
```

The CI workflow restores, builds, tests, and checks transitive NuGet dependencies for known vulnerabilities.

## Engineering decisions worth discussing

1. **Derive before asking.** Planning consumes reliable application facts instead of duplicating user input.
2. **One financial truth.** Dashboards and projections share the same position calculator and read model.
3. **Demo safety in depth.** Anonymous visitors receive a real tenant boundary and write attempts are rejected in the DbContext.
4. **Optional integrations stay optional.** Core planning and Today continue to work when providers are unavailable.
5. **Additive evolution.** Compact daily workflows coexist with advanced planning rather than replacing it.

## Public-showcase privacy

All demo identities, balances, transactions, goals, and timelines in this repository are synthetic. Production secrets and personal data are supplied only through external configuration and are not included here. See [SECURITY.md](SECURITY.md).

## Source availability

This repository is published for portfolio review. No open-source license is granted unless a license file is added later.
