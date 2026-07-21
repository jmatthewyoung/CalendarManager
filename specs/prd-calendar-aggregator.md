# PRD: Unified Calendar Aggregator (working name: "MasterCal")

## 1. Summary
A self-hosted calendar aggregation tool. Users log in with credentials managed in your own database (no third-party auth provider for login), then connect multiple Gmail and Outlook calendars via OAuth. The system merges all connected calendars into a single color-coded "master calendar" view. Delivered as one Angular PWA (installable, mobile-friendly) backed by a C#/.NET Web API, hosted on Azure. No native mobile app — the PWA covers that need. Web push notifications for reminders/sync events.

**Why home-built:** avoids handing calendar data to a third-party SaaS aggregator; you control the OAuth tokens, storage, and data retention end to end.

## 2. Goals / Non-Goals
**Goals**
- Single pane of glass across N Gmail + N Outlook calendars
- Color coding per source calendar (and per-event override)
- Own the auth + data layer, no third-party "calendar aggregator" service
- One Angular codebase, installable PWA, works well on phone and desktop
- Web push notifications (event reminders, sync failures)
- Runs on Azure alongside your other infrastructure

**Non-Goals (v1)**
- No native iOS/Android app (PWA only)
- No calendar *editing* sync-back to Google/Outlook — read-only ingestion only, permanently. Local events (§4.4) are the only way to add items from within the app.
- No team/shared-calendar permission system beyond single-user accounts (multi-user sharing is v2)
- No SSO/social login in v1 (email+password only, per your DB-managed auth requirement)

## 3. Users
- Primary: you (single-tenant initially), but design DB schema multi-tenant from day one since you'll likely want to add family/team members later.

## 4. Core Features (Functional Requirements)

### 4.1 Auth
- Register/login with email + password, stored in your own DB (ASP.NET Core Identity, hashed with the default PBKDF2/Argon2-based hasher)
- JWT access token + refresh token flow for the API (needed since Angular PWA + push both require token-based auth, not cookie-only)
- Password reset via email (Azure Communication Services or SendGrid)
- Optional but recommended: TOS/consent screen before OAuth connect (since you're touching third-party calendar data)

### 4.2 Calendar Connections
- "Connect a calendar" flow supporting:
  - Google Calendar (OAuth 2.0, `calendar.readonly` or `calendar.events` scope depending on write-back plans)
  - Microsoft Outlook/365 (Microsoft Graph API, OAuth 2.0, `Calendars.Read` or `Calendars.ReadWrite`)
- Support connecting **multiple accounts per provider** (e.g., 2 Gmail + 1 Outlook)
- Store refresh tokens encrypted at rest (Azure Key Vault for encryption keys, not just appsettings)
- Background sync job per connected calendar: Quartz.NET scheduled job polling every 10 minutes, fetching deltas since last sync per connection
- **Sync window: rolling 6 months back / 6 months forward** from "now," re-evaluated on every sync run (events that roll out of the window are pruned from the local cache but remain in the source provider untouched)
- Manual "resync now" action per calendar

### 4.3 Master Calendar View
- Merged view of all connected calendars: day / week / month / agenda views
- Each connected calendar gets an assigned color; user can change it
- Per-event color override
- Toggle visibility of individual calendars on/off without disconnecting
- Filter/search across merged events
- Timezone handling: store all events normalized to UTC, render in user's local timezone

### 4.4 Local/Native Events
- Ability to create events that live only in your DB (not synced anywhere) — useful since v1 has no write-back to Google/Outlook
- These render in the master view like any other calendar, with their own color

### 4.5 Web Push Notifications
- Browser push (Web Push API / VAPID) for:
  - Upcoming event reminders (configurable lead time)
  - Calendar sync failures (e.g., expired OAuth token needs re-auth)
- Requires user opt-in (browser permission prompt) and works even when the PWA is closed on supporting platforms (note: iOS Safari push support is more limited — flag as a known platform constraint)

### 4.6 PWA / Installability
- Web app manifest + service worker so the Angular app is installable on desktop and mobile home screens
- Offline shell (cached UI, last-synced data available offline; live sync resumes online)

## 5. Non-Functional Requirements
- **Security:** OAuth tokens encrypted at rest via Azure Key Vault; all traffic HTTPS; API rate limiting; audit log of connection/disconnection events
- **Reliability:** sync jobs retry with backoff; user notified on repeated failure
- **Performance:** master view should render sub-second for a typical week/month of merged events across 5-10 calendars
- **Data privacy:** this is the whole point of building it yourself — clearly document what's stored, and give users a "disconnect and delete data" action per calendar

## 6. Architecture

```
Angular PWA (frontend)
   │  HTTPS/JSON, Web Push
   ▼
ASP.NET Core Web API (C#)
   │
   ├── Auth module (Identity + JWT)
   ├── Calendar Sync module (Google/Graph SDK clients, background jobs)
   ├── Push Notification module (Web Push / VAPID)
   └── EF Core → SQL Server (Azure SQL)
   │
   ▼
Azure Key Vault (token encryption keys, secrets)
Quartz.NET (in-process scheduler, 10-min sync job per connection)
```

**Suggested Azure services:**
- Azure App Service (API) or Azure Container Apps if you want to containerize
- Azure Static Web Apps or App Service for the Angular PWA
- Azure SQL Database (fits your existing SQL Server familiarity)
- Azure Key Vault for secrets/token encryption
- Quartz.NET hosted inside the API as an `IHostedService`, running the sync job on a 10-minute cron trigger per connection (no separate Azure Functions app needed for this)
- Azure Notification Hubs *or* raw Web Push/VAPID (Notification Hubs is heavier than needed for browser-only push; raw VAPID via a library like `WebPush` in .NET is simpler for a PWA-only v1)

## 7. Tech Stack
- **Backend:** ASP.NET Core Web API (C#), EF Core, ASP.NET Core Identity, Quartz.NET (background sync scheduling)
- **Frontend:** Angular (latest LTS), Angular Service Worker (`@angular/pwa`) for installability, a calendar UI library (e.g., FullCalendar's Angular wrapper) rather than building the grid from scratch
- **DB:** Azure SQL Server
- **Integrations:** Google Calendar API (.NET client), Microsoft Graph SDK (.NET)
- **Hosting:** Azure (App Service / Container Apps + Azure SQL + Key Vault)
- **Push:** Web Push protocol with VAPID keys, `WebPush` NuGet package

## 8. Data Model (high level)
- `Users` (id, email, password hash, timezone, created_at)
- `CalendarConnections` (id, user_id, provider [google/outlook], account_email, encrypted_refresh_token, color, is_active, last_synced_at)
- `Events` (id, calendar_connection_id [nullable for local events], external_event_id, title, start_utc, end_utc, color_override, is_local, raw_payload_json)
- `PushSubscriptions` (id, user_id, endpoint, keys_json)
- `SyncLogs` (id, calendar_connection_id, status, message, ran_at)

## 9. Phased Roadmap
- **Phase 1 (MVP):** Auth, connect 1 Google + 1 Outlook calendar, Quartz.NET 10-min read-only sync, merged color-coded view, PWA installable shell
- **Phase 2:** Multiple accounts per provider, local events, web push reminders, manual resync
- **Phase 3:** Sync failure notifications, richer conflict/dedup handling, multi-user/shared calendars

Write-back to Google/Outlook and webhook-based push sync are explicitly out of scope — Quartz.NET polling every 10 minutes is the permanent sync mechanism.

## 10. Open Questions
_None outstanding — see §9 for sync window (rolling 6 months) and §12 for full technical design._
- Should disconnecting a calendar delete its cached events immediately, or retain history?

## 11. Design
For UI direction, I'd recommend using Claude to generate an Angular component mockup (or a clickable HTML prototype) directly once you're ready — I can build a working prototype of the master calendar view, color picker, and connection screen as a next step, styled deliberately rather than defaulting to generic Material/Bootstrap look. Say the word and I'll build that.

## 12. Technical Design
Full Clean Architecture breakdown (solution/project structure, entities, CQRS commands/queries, Quartz job design, API contract, Angular module layout, PWA/push implementation, Azure resource plan) is in the companion document: **technical-design.md**.
