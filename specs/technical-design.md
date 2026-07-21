# Technical Design: MasterCal (.NET Clean Architecture + Angular)

Companion to `prd-calendar-aggregator.md`. This covers solution structure, entities, sync job design, API contract, Angular structure, and Azure deployment.

---

## 1. Solution Structure (Clean Architecture)

```
MasterCal.sln
│
├── src/
│   ├── MasterCal.Domain/              # Entities, value objects, domain events, no dependencies
│   │   ├── Entities/
│   │   │   ├── User.cs
│   │   │   ├── CalendarConnection.cs
│   │   │   ├── CalendarEvent.cs
│   │   │   ├── PushSubscription.cs
│   │   │   └── SyncLog.cs
│   │   ├── Enums/
│   │   │   ├── CalendarProvider.cs      (Google, Outlook, Local)
│   │   │   └── SyncStatus.cs            (Success, Failed, AuthExpired)
│   │   ├── ValueObjects/
│   │   │   └── DateRangeUtc.cs
│   │   └── Common/
│   │       └── BaseEntity.cs, BaseAuditableEntity.cs
│   │
│   ├── MasterCal.Application/         # Use cases (CQRS), interfaces, no infra dependencies
│   │   ├── Common/
│   │   │   ├── Interfaces/
│   │   │   │   ├── IApplicationDbContext.cs
│   │   │   │   ├── ICalendarProviderClient.cs   (impl per provider in Infra)
│   │   │   │   ├── ITokenEncryptionService.cs
│   │   │   │   ├── IPushNotificationService.cs
│   │   │   │   └── ICurrentUserService.cs
│   │   │   ├── Behaviours/              # MediatR pipeline behaviours
│   │   │   │   ├── ValidationBehaviour.cs
│   │   │   │   └── LoggingBehaviour.cs
│   │   │   └── Mappings/ (AutoMapper profiles)
│   │   ├── CalendarConnections/
│   │   │   ├── Commands/
│   │   │   │   ├── ConnectCalendar/ (Command, Handler, Validator)
│   │   │   │   ├── DisconnectCalendar/
│   │   │   │   ├── ToggleCalendarVisibility/
│   │   │   │   └── SetCalendarColor/
│   │   │   └── Queries/
│   │   │       └── GetUserCalendars/
│   │   ├── Events/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateLocalEvent/
│   │   │   │   ├── UpdateLocalEvent/
│   │   │   │   ├── DeleteLocalEvent/
│   │   │   │   └── SetEventColorOverride/
│   │   │   └── Queries/
│   │   │       └── GetMergedCalendarView/  (date range -> merged events across sources)
│   │   ├── Sync/
│   │   │   ├── Commands/
│   │   │   │   └── SyncCalendarConnection/  (invoked by Quartz job)
│   │   │   └── Queries/
│   │   │       └── GetSyncLogs/
│   │   ├── PushSubscriptions/
│   │   │   └── Commands/
│   │   │       ├── RegisterPushSubscription/
│   │   │       └── RemovePushSubscription/
│   │   └── DependencyInjection.cs
│   │
│   ├── MasterCal.Infrastructure/       # EF Core, external APIs, Quartz, Key Vault
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/ (EF Core IEntityTypeConfiguration per entity)
│   │   │   └── Migrations/
│   │   ├── CalendarProviders/
│   │   │   ├── GoogleCalendarClient.cs      (implements ICalendarProviderClient)
│   │   │   ├── OutlookCalendarClient.cs     (implements ICalendarProviderClient, uses Graph SDK)
│   │   │   └── CalendarProviderClientFactory.cs
│   │   ├── Security/
│   │   │   └── KeyVaultTokenEncryptionService.cs
│   │   ├── Push/
│   │   │   └── WebPushNotificationService.cs   (VAPID via WebPush NuGet)
│   │   ├── BackgroundJobs/
│   │   │   ├── QuartzConfig.cs
│   │   │   ├── CalendarSyncJob.cs           (runs every 10 min per connection)
│   │   │   └── ReminderDispatchJob.cs       (checks upcoming events, triggers push)
│   │   └── DependencyInjection.cs
│   │
│   └── MasterCal.Api/                 # ASP.NET Core Web API (Presentation layer)
│       ├── Controllers/
│       │   ├── AuthController.cs
│       │   ├── CalendarConnectionsController.cs
│       │   ├── EventsController.cs
│       │   ├── PushController.cs
│       │   └── SyncController.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Program.cs
│       └── appsettings.json
│
├── tests/
│   ├── MasterCal.Domain.UnitTests/
│   ├── MasterCal.Application.UnitTests/     # handler tests with mocked interfaces
│   └── MasterCal.Api.IntegrationTests/      # WebApplicationFactory + Testcontainers (SQL)
│
└── angular/
    └── mastercal-web/                # Angular PWA (see §6)
```

**Dependency rule:** Domain has no references. Application references Domain only. Infrastructure references Application + Domain. Api references Application + Infrastructure. This keeps provider SDKs, EF Core, and Quartz entirely out of Domain/Application, so business logic is testable without spinning up real Google/Graph/SQL dependencies.

**Patterns used:** MediatR for CQRS (commands/queries + handlers), FluentValidation for request validation, AutoMapper for entity↔DTO projection.

---

## 2. Domain Entities

```csharp
// Domain/Entities/User.cs
public class User : BaseAuditableEntity
{
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string TimeZoneId { get; set; }        // IANA tz id, e.g. "America/Chicago"
    public ICollection<CalendarConnection> CalendarConnections { get; set; }
    public ICollection<PushSubscription> PushSubscriptions { get; set; }
}

// Domain/Entities/CalendarConnection.cs
public class CalendarConnection : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public CalendarProvider Provider { get; set; }     // Google, Outlook, Local
    public string AccountEmail { get; set; }
    public string EncryptedRefreshToken { get; set; }  // encrypted via Key Vault-backed DEK
    public string ColorHex { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public string? ProviderSyncCursor { get; set; }    // delta/sync token from provider, when supported
    public ICollection<CalendarEvent> Events { get; set; }
}

// Domain/Entities/CalendarEvent.cs
public class CalendarEvent : BaseAuditableEntity
{
    public Guid? CalendarConnectionId { get; set; }    // null => local-only event
    public Guid UserId { get; set; }
    public string? ExternalEventId { get; set; }
    public string Title { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? ColorOverrideHex { get; set; }
    public bool IsLocal { get; set; }
    public string? RawPayloadJson { get; set; }        // original provider payload, for debugging/rehydration
}

// Domain/Entities/PushSubscription.cs
public class PushSubscription : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public string Endpoint { get; set; }
    public string P256dhKey { get; set; }
    public string AuthKey { get; set; }
}

// Domain/Entities/SyncLog.cs
public class SyncLog : BaseAuditableEntity
{
    public Guid CalendarConnectionId { get; set; }
    public SyncStatus Status { get; set; }
    public string? Message { get; set; }
    public int EventsAdded { get; set; }
    public int EventsUpdated { get; set; }
    public int EventsRemoved { get; set; }
    public DateTime RanAtUtc { get; set; }
}
```

---

## 3. Sync Job Design (Quartz.NET)

**Registration** (`Infrastructure/BackgroundJobs/QuartzConfig.cs`):
```csharp
services.AddQuartz(q =>
{
    var jobKey = new JobKey("CalendarSyncJob");
    q.AddJob<CalendarSyncJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("CalendarSyncJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(10).RepeatForever()));

    var reminderKey = new JobKey("ReminderDispatchJob");
    q.AddJob<ReminderDispatchJob>(opts => opts.WithIdentity(reminderKey));
    q.AddTrigger(opts => opts
        .ForJob(reminderKey)
        .WithIdentity("ReminderDispatchJob-trigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));
});
services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);
```

**CalendarSyncJob logic (runs every 10 min):**
1. Query all `CalendarConnection` rows where `IsActive = true`.
2. For each connection, resolve the right provider client via `CalendarProviderClientFactory` (Google or Outlook).
3. Compute the sync window: `[UtcNow.AddMonths(-6), UtcNow.AddMonths(6)]`.
4. Call provider API for events in that window (use delta/sync tokens where the provider supports them — Google's `syncToken` and Graph's `delta` query — to avoid always fetching the full 12-month window; fall back to full fetch if the token is invalidated).
5. Diff against locally cached `CalendarEvent` rows for that connection: insert new, update changed, soft-delete/prune any that fell outside the 6-month window or were removed at the source.
6. Write a `SyncLog` row with counts and status.
7. On auth failure (refresh token revoked/expired): mark connection `IsActive = false` is **not** automatic — instead set a `NeedsReauth` flag (add to `CalendarConnection`), log `SyncStatus.AuthExpired`, and enqueue a push notification via `ReminderDispatchJob`'s notification path so the user is told to reconnect.
8. Each connection's sync runs independently and is wrapped in try/catch so one broken connection doesn't block others in the same job tick.

**ReminderDispatchJob logic (runs every 5 min):**
1. Query events starting within the next {user's configured lead time, default 10 min} that haven't already had a reminder dispatched (track via a `ReminderSentAtUtc` column or a lightweight dedup table).
2. For each, look up the user's `PushSubscription` rows and send a Web Push payload via `IPushNotificationService`.

**Idempotency:** every sync run is safe to re-run — matching is done on `ExternalEventId` + `CalendarConnectionId`, so a retry after a partial failure won't duplicate events.

---

## 4. API Contract (high level)

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Create account (email + password) |
| POST | `/api/auth/login` | Returns JWT access token + refresh token |
| POST | `/api/auth/refresh` | Exchange refresh token for new access token |
| POST | `/api/auth/logout` | Revoke refresh token |
| GET | `/api/calendar-connections` | List user's connected calendars |
| POST | `/api/calendar-connections/google` | Start Google OAuth flow (redirect/callback) |
| POST | `/api/calendar-connections/outlook` | Start Outlook OAuth flow (redirect/callback) |
| DELETE | `/api/calendar-connections/{id}` | Disconnect a calendar (and optionally purge cached events) |
| PATCH | `/api/calendar-connections/{id}/color` | Update color |
| PATCH | `/api/calendar-connections/{id}/visibility` | Toggle show/hide |
| POST | `/api/calendar-connections/{id}/resync` | Manual resync trigger (enqueues immediate job run, not just waiting for the 10-min tick) |
| GET | `/api/events?start=&end=` | Merged calendar view for a date range |
| POST | `/api/events` | Create local event |
| PUT | `/api/events/{id}` | Update local event (local events only) |
| DELETE | `/api/events/{id}` | Delete local event |
| PATCH | `/api/events/{id}/color` | Per-event color override |
| GET | `/api/sync/logs` | Recent sync history/status per connection |
| POST | `/api/push/subscribe` | Register a Web Push subscription |
| DELETE | `/api/push/subscribe` | Remove a Web Push subscription |

All routes except `auth/register`, `auth/login`, `auth/refresh` require a valid JWT bearer token.

---

## 5. Auth Flow
1. Register/login issue a short-lived JWT access token (e.g., 15 min) + a longer-lived refresh token (e.g., 14 days), refresh token stored hashed in DB with rotation on use.
2. Angular stores the access token in memory (not localStorage, to reduce XSS exposure) and silently refreshes via an HttpOnly refresh-token cookie or a secure refresh endpoint call on app load.
3. OAuth to Google/Outlook is a **separate** flow from app login — it happens after the user is already authenticated, to link a calendar to their existing MasterCal account. Standard authorization-code + PKCE flow; the callback lands on the API, which exchanges the code, encrypts the refresh token (via a Key Vault-backed key), and stores the `CalendarConnection`.

---

## 6. Angular App Structure

```
angular/mastercal-web/
├── src/
│   ├── app/
│   │   ├── core/                     # singletons: auth, http interceptors, guards
│   │   │   ├── auth/ (auth.service.ts, token-refresh.interceptor.ts, auth.guard.ts)
│   │   │   └── push/ (push.service.ts — subscribes SW registration to backend)
│   │   ├── features/
│   │   │   ├── calendar/             # master calendar view, day/week/month
│   │   │   ├── connections/          # connect/manage calendars, color picker
│   │   │   ├── auth/                 # login/register pages
│   │   │   └── settings/
│   │   ├── shared/                   # shared UI components (Angular Material based)
│   │   └── app.config.ts
│   ├── manifest.webmanifest          # PWA install metadata
│   ├── ngsw-config.json              # Angular service worker caching rules
│   └── sw-push-listener.js           # custom push event handling on top of ngsw
```

- **PWA:** `@angular/pwa` schematic adds the service worker + manifest; `ngsw-config.json` configures caching (app shell cached, API calls network-first with a short cache fallback for offline viewing).
- **Push:** on first login, `push.service.ts` requests Notification permission, gets the `PushSubscription` from the browser via the SW registration, and POSTs it to `/api/push/subscribe`.
- **State:** a signal-based or NgRx-lite store for calendar visibility toggles and colors, kept in sync with the backend on change (optimistic UI update, then PATCH).
- **UI kit:** Angular Material, consistent with the Material 3 prototype already built.

---

## 7. Azure Deployment Plan

| Component | Azure Service |
|---|---|
| API (Clean Architecture solution) | App Service (Linux, .NET 8/9) or Container Apps if you containerize |
| Angular PWA | Azure Static Web Apps (or same App Service, serving the built Angular dist) |
| Database | Azure SQL Database |
| Secrets / token encryption keys | Azure Key Vault |
| Background jobs | Quartz.NET runs **in-process** inside the API's App Service — no separate compute needed, since App Service "Always On" keeps it alive |
| Push | No extra Azure service required — Web Push/VAPID goes directly browser↔push service (Google/Mozilla push endpoints), API just signs payloads |
| Monitoring | Application Insights (wired into both Api and Quartz jobs for sync success/failure telemetry) |

**Important App Service setting:** enable **"Always On"** — without it, App Service can idle/unload the app after inactivity, which would silently stop the in-process Quartz scheduler. This is the one setting that's easy to miss and would look like "sync just stops working."

---

## 8. Testing Strategy
- **Domain/Application unit tests:** MediatR handlers tested with mocked `ICalendarProviderClient`, `IApplicationDbContext` (EF Core InMemory or SQLite for repository-style tests).
- **Provider client tests:** Infrastructure-level tests against recorded/mocked Google & Graph API responses (don't hit live APIs in CI).
- **Integration tests:** `WebApplicationFactory` + a real (or Testcontainers) SQL instance, covering the full auth → connect → sync → merged view path.
- **Quartz job tests:** trigger `CalendarSyncJob.Execute` directly in tests with a mocked provider client returning a fixed event set, assert insert/update/prune behavior against the 6-month window boundary.
