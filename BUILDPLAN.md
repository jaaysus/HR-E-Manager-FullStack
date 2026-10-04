Coat catalog update: cards and forms now use Name, Color, and Size. Item codes are generated internally. Apply `CoatColorAndSize`; legacy coats keep empty color/size until edited. Stock menus and movement history show the clothing details. Employee eligibility displays the next due time in the business timezone. Future enrollment postpones the first minute-based request until one minute after the enrollment date begins; it does not bypass enrollment. Clothing snapshots refresh every ten seconds.

# Current clothing requirements (October 4, 2026)

Department and season assignment rules are removed from the active API and UI. Coat variants are an editable inventory catalog used to trace the clothing taken, independently of employee department or calendar season. Inventory managers create, rename, retire, and restore variants; retirement requires zero stock and preserves references from requests and the ledger. The providing user selects any active stocked variant. The API completes the request and deducts stock in one transaction.

Clothing settings configure `eligibilityInterval` (1 to 10000) and `eligibilityUnit` (`Minutes`, `Days`, `Months`), defaulting to six months. Changing either starts a new schedule at save time. Existing request cycle numbers are reserved so completed allocations cannot be duplicated after a policy change. The first request under the new policy is due after one interval. Minute/day intervals use elapsed time; changed month policies use month anniversaries. The original default preserves calendar-month eligibility. Only the current outstanding cycle is generated; older requests are superseded. The worker checks every ten seconds, with snapshots also reconciling on demand. Choose 1 Minute for testing. Historic department/season columns and rule rows remain archival only and are not used to select clothing.

Apply `ConfigurableClothingEligibility` with `dotnet ef database update --project backend` before using this version. The historical build notes below describe earlier requirements and are superseded by this section.

# HR E-Tracker build plan

## Settings scopes and sidebar repair (2026-10-04)

Settings are now separated by ownership. My account contains profile name/email, assigned roles, password, and personal clothing-feed preferences with independent Save actions. Organization settings contains company, business time zone, and department management. Clothing settings lives under Clothing tracking: module-wide low-stock alerts/threshold, the fixed six-month/calendar-season policy, and effective-dated department/item rule management. Clothing Manager has `clothing.settings.manage`; Clothing Viewer and existing Inventory Manager can read clothing settings. HR-only roles cannot access that page or its API. HR Administrator has `platform.departments.manage` for creating, renaming, and activating/deactivating departments. Departments with active employees cannot be deactivated.

`GET/PUT /api/settings` owns company/time zone/rowVersion; reads also return the server's business date for the header. `GET/PUT /api/clothing/settings` contains only low-stock settings/rowVersion and the informational six-month interval. `GET/PUT /api/auth/profile/preferences` persists the current user's feed choices. Organization/clothing writes cannot modify each other's fields, and row versions prevent overwrites. Personal preferences filter feed, unread counts, and program snapshots without changing global alert generation. The account/password/organization/clothing/departments/rule saves have separate forms and outcomes. Employee forms now use active departments from the API. Employee writes and department changes share the transaction guard so creation cannot race department deactivation.

Applied migration `20261004133729_SeparatePersonalNotificationPreferences` adds two user preferences defaulting to true. The backend server remains stopped. New sidebar layout keeps identity and user/sign-out controls within the viewport, scrolls navigation independently, and scrolls long page content inside the main area. Navigating to another page resets its scroll position.

Validation: 64 default backend tests and eight SQL Server tests pass, including scope isolation, personal-feed filtering, department uniqueness/deactivation, and concurrent SQL settings writes. Twelve frontend tests, typecheck/build, and EF model validation pass. Browser role fixtures cover five role combinations, separate settings saves, department/rule controls, and a 120-row ledger at a 650px viewport; no JavaScript errors. Seven retained demo screens still match main layout dimensions/table headers. Organization/account/clothing settings intentionally replace the old combined demo settings. Evidence: `artifacts/rbac/browser-checks.json` and `artifacts/demo-comparison/visual-comparison.json`.

## Current access model: HR workspace and clothing module (2026-10-04)

The latest user instruction restores the HR workspace as the application home. Clothing tracking is a separately authorized module, retaining the restored demo layouts within its screens. Users & access is visible in Workspace navigation. HR-only employees use the employee directory without clothing status or stock data. Platform pages load independently of the clothing snapshot, which is fetched only on authorized clothing views.

| Role | HR employees | Users / organization management | Clothing tracking |
| --- | --- | --- | --- |
| `HrAdministrator` | Manage / import | Yes | Requires an additional clothing role |
| `Viewer` | Read | No | None |
| `ClothingManager` | Manage / import | No | Manage stock, rules, requests, and provision |
| `ClothingViewer` | Read | No | Read only |
| `InventoryManager` | Read | No | Existing role: read, stock receipts/adjustments, provision |

Roles combine explicitly. Assign `ClothingManager` or `ClothingViewer` through Users & access; an HR administrator may assign a clothing role to their own account. Updating roles revokes existing sessions and requires sign-in again. Startup ensures all built-in roles exist even when administrator bootstrap is cached; existing accounts are not automatically granted the new clothing roles. The API enforces these grants, and clothing alerts are excluded from notification feeds/read actions for HR-only accounts.

Verification: 56 default backend integration tests pass (seven SQL tests are opt-in), including separate HR/clothing permissions, notification isolation, role assignment/removal, and session revocation. Twelve frontend tests and typecheck/build pass. `python scripts/verify-rbac.py` verifies workspace landing, visible modules, user-role controls, and stock action guards for four role fixtures without starting the backend. Browser evidence is in `artifacts/rbac/browser-checks.json`. Earlier landing-page/role descriptions below are historical and superseded by this section.

## Current implementation: demo restoration (2026-10-04)

The user's latest requirement is a faithful, functional version of the external HR E-Manager demo. This supersedes the earlier workspace landing page, historical catch-up, exact-day anniversary, and immutable employee-number decisions recorded below. The application now opens on Dashboard and restores the demo's screens, navigation, employee coat-status filters, eligibility context, inventory cards/combined ledger, request summaries, notifications, imports, and settings.

Eligibility uses calendar month differences, ignoring the enrollment day, as in the demo: `floor(monthsSinceEnrollment / 6)`. Only the current outstanding cycle is generated. Older outstanding cycles are superseded; current requests follow today's department/season rules. Provided cycles remain immutable and cannot be issued twice. A later enrollment correction cannot create another issue behind an already completed cycle. The worker reconciles every five minutes; snapshots and provision also reconcile before proceeding.

The restored UI reads one server-authoritative program snapshot. Real CSV/XLSX/XLS uploads, persisted previews and commits, editable unique employee numbers, dated stock receipts, transactional provision, per-user notification reads, account name/email, company name, and low-stock settings replace the demo's in-memory or simulated controls. Authentication, server permissions, concurrency protection, and user management remain available. Charts use actual allocation history. The default low-stock threshold is 15.

Applied migration: `20261004113518_RestoreDemoProgramSettings` (company name and demo threshold). Existing business data was retained; no reset was necessary.

Validation: frontend typecheck, 12 frontend tests, production build, backend build without warnings, 50 default backend tests, and all seven opt-in SQL Server tests pass. Eight live browser checks passed against the local API/SQL Server, including reload persistence, import, stock dates, provision, and settings. Eight screens matched the demo's main layout dimensions and table headers using identical fixtures, with no browser JavaScript errors. These layout checks do not claim pixel identity; identity/date, real chart values, permissions, and import validation intentionally reflect working backend behavior. Evidence and reproducible scripts are linked in `FUNCTIONALITY-COMPARISON.md`.

Earlier delivery entries below are historical; the current behavior above takes precedence.

## Purpose

Build HR E-Tracker as a modular HR application backed by ASP.NET Core and SQL Server. Inventory is one feature among many. Employee records, inventory, and the workwear request program are the first modules; future HR features join the same authenticated workspace. The frontend lives in `frontend/` and the API and tests in `backend/`.

The existing demo records are mock data. They may be changed, removed, or replaced during implementation; preserving them is not required. An empty, working application is acceptable and preferred when it speeds delivery. Seed only required reference data (departments, coat catalog, and rules), rather than mock employees, stock, requests, or notifications.

## Application modules and RBAC

Authentication, account administration, departments, business time zone, audit infrastructure, and the workspace shell are shared platform capabilities. Employee records/imports belong to the Employees module; catalog, balances, receipts, adjustments, movements, and item rules belong to Inventory; eligibility cycles, coat requests, provision, and allocation reporting belong to Workwear. A workwear issue uses Inventory's transactional stock workflow. Future modules must not put their business rules in inventory services or turn the coat dashboard into the application home.

The application opens on a permission-filtered workspace. Its navigation registry declares the module and required permission for every feature. Session/query infrastructure is shared, while services and query keys are grouped by module. Keep one deployable API and one database for now; module boundaries do not require microservices or separate databases.

RBAC is mandatory on the server. The current three Identity roles map to explicitly named permissions in `backend/Models/AccessPolicies.cs`. Endpoints enforce those named policies; login, refresh, and `/api/auth/me` expose effective permissions for frontend page and action guards. Browser visibility is only a usability layer. Unknown roles receive no module grants, and new modules require explicit grants even for an administrator. No wildcard permission or automatic grant exists.

| Permission | HrAdministrator | InventoryManager | Viewer |
| --- | --- | --- | --- |
| `platform.users.manage` | Yes | No | No |
| `platform.departments.read`, `platform.notifications.read`, `platform.settings.read` | Yes | Yes | Yes |
| `platform.settings.manage` | Yes | No | No |
| `employees.read` | Yes | Yes | Yes |
| `employees.manage`, `employees.import` | Yes | No | No |
| `inventory.read` | Yes | Yes | Yes |
| `inventory.manage` | Yes | Yes | No |
| `inventory.rules.manage` | Yes | No | No |
| `coats.read`, `coats.dashboard.read` | Yes | Yes | Yes |
| `coats.provide` | Yes | Yes | No |
| `coats.manage` | Yes | No | No |

Administrators can create accounts, assign multiple built-in roles, and deactivate accounts through Users & access. Editing users invalidates their sessions; the API protects the last active administrator. Custom role definitions and database-managed permission grants are a later extension, not part of this increment. Before adding another module, define its permission namespace, policies, services, API contracts, query keys, and navigation entries. Scope its notifications and settings to that module's grants; the current shared notification feed contains only the existing features' alerts. Split workwear preferences from the legacy `/settings` contract when another module needs its own settings.

Policy enforcement follows [ASP.NET Core named authorization policies](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0). Server query caching and invalidation use [TanStack Query](https://tanstack.com/query/latest/docs/framework/react/overview).

## Demo findings

| Area | Current demo behaviour | Backend requirement |
| --- | --- | --- |
| Authentication | Login accepts only `admin@mail.com` and `Lear123456@` in the browser. | Replace with server-side Identity accounts, secure password hashing, JWT or secure cookie sessions, and role-based authorization. Never retain demo credentials. |
| Employees | Create and edit update React state only. Employee IDs are generated randomly and are not validated for uniqueness. | Persist employees with a unique employee number, validation, audit fields, paging, filtering, and optimistic concurrency. |
| Imports | “Select File” only switches to a hard-coded preview. Confirm has no action. | Upload CSV/XLSX, validate every row, return a preview with errors, then import atomically or with an explicitly documented partial-import policy. |
| Eligibility | A request cycle is calculated in the UI as `floor(months since enrollment / 6)`. | Generate durable requests for every due cycle through an idempotent scheduled job. The schedule is fixed at six months. |
| Coat selection | Department and current month determine a coat variant. Warehouse uses hi-vis; Operations/Logistics use logistics summer; Maintenance uses summer from April through September; Quality uses winter; Production uses summer from April through September and winter otherwise. | Store department-to-item and season rules as data, resolve the required variant at request creation, and retain the selected variant on the request. |
| Inventory | Stock equals the sum of movement quantities. Arrivals are positive; allocations are `-1`. | Keep an immutable stock ledger, record an allocation when a request is issued, and ensure stock cannot become negative under concurrent requests. |
| Requests | Active requests are derived at render time; “Mark as Provided” mutates employee state, inventory, and notifications independently. | Persist request lifecycle and issue within one transaction: verify eligibility/status, reserve or allocate one item, create ledger row, complete request, and create notification/audit event. |
| Notifications | Read state exists only in memory. Low-stock alerts are a UI calculation. | Persist notifications and preferences; generate low-stock alerts when inventory crosses the configured threshold, without duplicate alert floods. |
| Settings | The six-month interval is visually fixed; alert preference and threshold are not saved. | Persist organization-level low-stock preference and threshold. Keep the eligibility interval fixed at six months unless policy formally changes. |
| Dashboard | KPIs and bar chart contain partly derived and partly fixed data. | Query real employee, request, inventory, notification, and monthly allocation data. |

## Business rules to preserve

1. An employee is due at the six-month anniversary of enrollment and every six months after that.
2. A cycle can have at most one request per employee. A unique key on `(EmployeeId, CycleNumber)` enforces this rule.
3. A completed cycle must not create another pending request. New cycles still become due.
4. One issued coat consumes one unit of the request’s assigned inventory item.
5. An employee cannot be issued a coat for a cancelled, completed, or out-of-stock request.
6. When an item has no available quantity, the request remains pending with an out-of-stock state; it must become allocatable after replenishment.
7. Employee numbers are business identifiers, distinct and immutable after creation unless an administrator executes a controlled correction.
8. Dates are stored as UTC timestamps where time matters. Enrollment is a `date`; the six-month calculation uses the organization’s configured business time zone.

## Target architecture

```text
React/Vite frontend  ── HTTPS + access token/cookie ──>  ASP.NET Core Web API
frontend/                                                  backend/HrETracker.Api
                                                               │
                                                               ├── application services
                                                               ├── EF Core migrations
                                                               ├── scheduled eligibility worker
                                                               └── SQL Server
```

Use .NET 10 LTS, ASP.NET Core, EF Core with `Microsoft.EntityFrameworkCore.SqlServer`, ASP.NET Core Identity, OpenAPI, and a test project. .NET 10 is supported through November 2028 according to the [official support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), and EF Core’s SQL Server provider is selected through `UseSqlServer` ([Microsoft documentation](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/)).

Use a conventional, single-project ASP.NET Core API structure, matching the existing `DynamicWiApi` project. Keep HTTP endpoints, models, EF Core persistence, services, and utilities together in one deployable backend project. This makes the application easy to navigate from Visual Studio and avoids project-to-project references for this scale of system.

```text
backend/
  HrETracker.sln
  HrETracker.Api/
    Controllers/                # API endpoints and request/response models
    Data/                       # DbContext, Entity Framework configuration, seed data
    Models/                     # entities, enums, DTOs, and view models
    Services/                   # business workflows and scheduled worker
    Utils/                      # shared helpers, mappings, validation support
    Migrations/                 # EF Core migrations
    Properties/                 # launch settings
    wwwroot/                    # optional static content; do not store permanent uploads here
    uploads/                    # local development import staging only; ignored by Git
    Program.cs                  # configuration, dependency injection, middleware, route setup
    appsettings.json
    appsettings.Development.json
    HrETracker.Api.csproj
  tests/
    HrETracker.Api.Tests/       # unit and integration tests when added
```

`Controllers` should remain thin: authorize, validate HTTP input, call a service, and return an HTTP result. `Services` own the employee, import, inventory, request-cycle, notification, and dashboard workflows. `Data` owns all SQL Server and EF Core concerns. `Models` contains the database entities and API contracts until the API grows enough to warrant separating DTOs into their own folder.

## SQL Server data model

Every mutable aggregate has `CreatedAtUtc`, `CreatedByUserId`, `UpdatedAtUtc`, `UpdatedByUserId`, and a SQL Server `rowversion` column. Return row versions as opaque values and require them on update requests. EF Core maps SQL Server `rowversion` as an optimistic concurrency token ([Microsoft guidance](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)).

| Table | Essential fields and constraints |
| --- | --- |
| `AspNetUsers`, `AspNetRoles`, related Identity tables | Standard ASP.NET Core Identity tables. Roles: `HrAdministrator`, `InventoryManager`, `Viewer`. |
| `Departments` | `Id`, unique `Code`, `Name`, `IsActive`. Seed the six departments shown in the demo. |
| `Employees` | `Id` (UUID), unique `EmployeeNumber`, `FullName`, `DepartmentId`, `JobTitle`, `EnrollmentDate`, `Notes`, `IsActive`, audit columns, row version. Index `(DepartmentId, IsActive)` and search-friendly employee number/name indexes. |
| `InventoryItems` | `Id`, unique `Sku`, `Name`, `DepartmentId` nullable, `Season` (`Winter`, `Summer`, `AllSeason`), `IsActive`, display metadata. Seed the seven demo variants. |
| `DepartmentItemRules` | `Id`, `DepartmentId`, `Season`, `InventoryItemId`, effective date range, unique active rule per department and season. Operations and Logistics may share an item rule. |
| `InventoryBalances` | `InventoryItemId` primary key, `QuantityOnHand`, row version. Maintained only by transactionally posting ledger movements. |
| `InventoryMovements` | `Id`, `InventoryItemId`, `OccurredAtUtc`, `Type` (`Receipt`, `Allocation`, `Adjustment`, `Reversal`), signed `Quantity`, `RequestId` nullable, `Reference`, `Note`, actor/audit data. Unique `RequestId` for allocation movements. |
| `CoatRequests` | `Id`, `EmployeeId`, `CycleNumber`, `DueDate`, `InventoryItemId`, `Status` (`Pending`, `OutOfStock`, `Provided`, `Cancelled`), `RequestedAtUtc`, `ProvidedAtUtc` nullable, `ProvidedByUserId` nullable, notes, row version. Unique `(EmployeeId, CycleNumber)`. |
| `Notifications` | `Id`, `RecipientUserId` nullable, `Type`, `Message`, `RelatedEntityType`, `RelatedEntityId`, `CreatedAtUtc`, `ReadAtUtc` nullable. Add a deduplication key for scheduled/system alerts. |
| `OrganizationSettings` | singleton row: `TimeZoneId`, `LowStockAlertsEnabled`, `LowStockThreshold`, audit fields, row version. |
| `ImportBatches` and `ImportRows` | original file metadata, uploader, status, validation summary, row number, normalized row data, validation errors, and import outcome. Files are stored outside the database or as approved object storage references. |
| `AuditEvents` | append-only actor, action, entity type/id, timestamp, correlation ID, and redacted before/after JSON. |

The database enforces positive receipt quantities, nonzero movement quantities, valid status transitions in application code, unique employee numbers, and one request/allocation per cycle. Allocation must update `InventoryBalances` using a guarded statement (`QuantityOnHand >= 1`) inside the same transaction as the request and ledger change. Do not calculate availability only from a stale client value.

## API contract

All API routes are under `/api`, return RFC 9457 problem details for errors, and require authentication except the login/refresh/logout endpoints. The `/health` probe is also public. Use OpenAPI to publish the exact schema.

| Area | Endpoints |
| --- | --- |
| Auth | `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me`, `POST /auth/change-password`. If corporate SSO is available, add OIDC before deploying passwords. |
| Users | Administrator-only `GET /users?page=&pageSize=`, `GET /users/{id}`, `POST /users`, `PUT /users/{id}` (name, active status and roles). Email is fixed after creation. |
| Employees | `GET /employees?query=&departmentId=&status=&page=&pageSize=`, `POST /employees`, `GET /employees/{id}`, `PUT /employees/{id}`, `PATCH /employees/{id}/active`. |
| Import | `POST /employee-imports` multipart upload, `GET /employee-imports?page=&pageSize=` history, `GET /employee-imports/{id}?page=&pageSize=` preview/errors, `POST /employee-imports/{id}/commit`. |
| Inventory | `GET /inventory/items`, `GET /inventory/items/{id}/movements`, `POST /inventory/receipts`, `POST /inventory/adjustments`. |
| Requests | `GET /coat-requests?status=&departmentId=&page=`, `GET /coat-requests/{id}`, `POST /coat-requests/run-due-cycle` (administrator/manual recovery), `POST /coat-requests/{id}/provide`, `POST /coat-requests/{id}/cancel`. |
| Notifications | `GET /notifications`, `POST /notifications/{id}/read`, `POST /notifications/read-all`. |
| Settings & dashboard | `GET/PUT /settings`, `GET /dashboard?year=`. |

Role rules: administrators manage users, settings, imports, employees, and requests; inventory managers receive stock, adjust inventory, and provide coats; viewers have read access. Identity supports ASP.NET Core authentication and role-based authorization ([Microsoft documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)).

## Critical workflows

### Daily due-cycle worker

1. Run once each business day after the configured time-zone date begins; expose a protected manual run for recovery.
2. Find active employees whose current cycle number is at least one.
3. Insert missing `(EmployeeId, CycleNumber)` records only; a unique constraint makes retries safe.
4. Resolve and save the item rule at creation time. If no rule exists, create an operations notification and flag the request for configuration, rather than guessing.
5. Set status to `Pending` when the item has stock or `OutOfStock` when it does not. Create a deduplicated notification for newly-created requests.

### Provide a coat

1. Authorize the user and load the request, item balance, and row versions.
2. Reject any request not in `Pending` or `OutOfStock`, a request without an assigned item, or unavailable stock.
3. Begin a transaction. Atomically decrement balance only where at least one unit is available.
4. Insert an `Allocation` movement, mark the request `Provided`, record the provider and time, write audit and success notification rows, then commit.
5. Return HTTP 409 for a stale row version or stock race; the frontend refreshes the request.

EF Core applies a single `SaveChanges` call transactionally for providers that support transactions, but this multi-aggregate workflow should make the transaction boundary explicit ([Microsoft documentation](https://learn.microsoft.com/en-us/ef/core/saving/transactions)).

### Inventory receipt and low-stock alerts

Record a positive receipt and increment the balance in one transaction. After every balance-changing operation, evaluate the organization threshold per item and create only one unresolved low-stock notification for an item. Resolve or close that alert when the balance recovers.

### Import flow

Accept only `.csv`, `.xlsx`, and `.xls` up to 10 MB. Virus-scan or quarantine uploaded files when infrastructure provides it. Parse into staged rows, normalize Excel dates, validate required fields (`Employee ID`, `Full Name`, `Department`, `Enrollment Date`), validate department/rules, and identify duplicate employee numbers both within the file and database. The commit endpoint imports only a successfully validated batch, is idempotent, and records an audit event.

## Frontend integration plan

1. Complete the frontend file restructuring below before API integration, preserving the current UI and behavior. Verify with frontend typecheck/build.
2. Add a typed API client in `frontend/src/services` using a configurable `VITE_API_BASE_URL`.
3. Replace seed arrays and component state with server queries and mutations. Keep transient UI state local.
4. Replace hard-coded login with the API login flow and route guard; remove exposed credentials.
5. Map server request statuses and row-version conflicts into the existing badge and action UI.
6. Use the import batch preview endpoint in place of the current static three-row preview.
7. Add loading, empty, validation, network-error, and conflict states for each view.

### Frontend file restructuring (completed)

Split `App.tsx` into the following layout. Keep `App.tsx` responsible for composing authentication, navigation, the layout, and the active page. Keep form inputs, filters, dialogs, and upload steps local to their pages.

```text
frontend/src/
├── main.tsx
├── App.tsx
├── index.css
├── app/
│   ├── navigation.ts           # Screen names, menu items, titles
│   ├── AuthProvider.tsx        # In-memory session and effective permissions
│   └── queryClient.ts         # Shared server-query defaults
├── layouts/
│   ├── AppLayout.tsx           # Sidebar, header, content, footer
│   ├── Sidebar.tsx
│   └── Header.tsx
├── pages/
│   ├── LoginPage.tsx
│   ├── DashboardPage.tsx
│   ├── employees/
│   │   ├── EmployeesPage.tsx
│   │   └── EmployeeForm.tsx    # Shared by add and edit
│   ├── ImportPage.tsx
│   ├── InventoryPage.tsx
│   ├── RequestsPage.tsx
│   ├── NotificationsPage.tsx
│   ├── SettingsPage.tsx
│   ├── UsersPage.tsx
│   └── AccountPage.tsx
├── components/
│   ├── LearLogo.tsx
│   ├── Icons.tsx
│   ├── StatusBadge.tsx
│   ├── ApiState.tsx            # Loading, validation, and conflict messages
│   ├── Pagination.tsx
│   └── Toggle.tsx
├── services/
│   ├── apiClient.ts            # Base URL, requests, API error handling
│   ├── employeesApi.ts
│   ├── inventoryApi.ts
│   ├── coatsApi.ts
│   └── platformApi.ts
├── types/
│   └── api.ts                 # Typed API contracts and permission names
└── utils/
    └── dates.ts               # Date formatting and display helpers
```

Perform this in two passes:

- First extract the existing screens, layout, shared components, navigation, and demo types. Keep dashboard-only components such as `KpiCard` and `MiniBarChart` in `DashboardPage.tsx` initially. Temporarily isolate seed data and demo business rules in `demo/`; preserve behavior during extraction.
- During the subsequent API integration step, add `AuthProvider.tsx`, the service files, and API contract types as they are needed. Replace demo request status, coat selection, and stock calculations with server results, then remove `demo/`. Avoid empty placeholder files during the initial restructuring.

Use React local state for transient UI state and React Context for the session. Redux is not required. Use TanStack Query during API integration for server queries, caching, mutations, and refreshing affected views after successful writes.

## Delivery sequence

- [x] Move the React/Vite application into `frontend/` and establish `backend/`.
- [x] Create the .NET 10 solution and single API project with `Controllers`, `Data`, `Models`, `Services`, `Utils`, and `Migrations`.
- [x] Configure local SQL Server development settings, EF Core SQL Server, ASP.NET Core Identity types, CORS, a health endpoint, an initial employee entity/service/controller, and the initial migration.
- [x] Configure authentication end-to-end: identity user management, roles, administrator bootstrap, token/session strategy, and authorization policies.
- [x] Implement departments, employee lifecycle, pagination, filtering, search, validation, auditing, and required department reference seed data. Mock employee seed data is not required.
- [x] Implement inventory items, department/season item rules, balances, receipts, adjustments, stock ledger, and low-stock alerts.
- [x] Implement six-month request generation, request lifecycle, transactional coat provision, concurrency protection, and request notifications.
- [x] Implement dashboard queries and monthly issue reporting from stored data.
- [x] Implement staged CSV/XLSX import, row validation/preview, idempotent commit, and import audit history.
- [x] Restructure the frontend into the documented `app`, `layouts`, `pages`, `components`, `types`, and `utils` layout; isolate temporary demo data/rules and pass frontend typecheck/build before API integration.
- [x] Wire the React app to the API, remove seed data and hard-coded credentials, and add loading, error, empty, and conflict states.
- [x] Establish the shared HR workspace, module-aware navigation, explicit permission policies, frontend guards, and Users & access.
- [ ] Verify the complete browser workflow against the running SQL Server API, including reconnect/reload, viewer restrictions, and stale-write recovery.
- [ ] Add tests and CI: frontend typecheck/build; backend restore/build/test; migration validation; integration tests against SQL Server.

## Acceptance checks

- [x] A newly enrolled employee produces exactly one request at six months and exactly one per later six-month cycle, even if the worker runs repeatedly.
- [x] Two simultaneous provide attempts cannot issue more coats than available stock.
- [x] Providing a coat yields exactly one request completion, allocation movement, notification, and audit event.
- [x] Import previews row-level validation errors and rejects invalid batches; retrying a committed batch returns its saved result without importing again.
- [ ] Employee search/filter, dashboard KPIs, inventory totals, request list, notification read state, and settings all survive a restart.
- [x] Unauthorized users receive 401; authenticated users without the required role receive 403.
- [x] Updates containing an obsolete row version receive a clear 409 response.
- [ ] Database connection strings, secrets, passwords, and access tokens are absent from source control and logs.

## Confirmed implementation decisions

### Frontend API integration and module access (2026-10-04)

All active views now use typed API services and TanStack Query: employees and department filters, employee forms and active status, stock balances/receipts/adjustments/history, paginated coat requests and provision/cancellation/item assignment/manual generation, dashboard reports, per-user notification reads, settings, staged employee import preview/commit/history, and user administration. The workspace is the application home; the coat dashboard is a Workwear feature. Demo arrays, browser eligibility calculations, and the hard-coded login check are removed.

The auth provider keeps access tokens in memory and restores the session on reload using a persistent HttpOnly refresh cookie. Login and refresh return access tokens and user permissions without exposing refresh tokens in JSON. Cookie auth endpoints require X-HR-Auth: 1, and exact allowed origins support credentialed CORS. Cookies use SameSite=Strict, path /api/auth, and Secure outside local HTTP development. Startup waits for restoration; expired sessions show sign-in and connection failures offer retry. Concurrent API calls share a single refresh, retry authentication once, and discard responses from an older session. Permission failures and write conflicts are not automatically retried. Logout clears the local session even if server revocation fails; in that case the existing server-side expiry still applies. Query caches clear at session changes. Password changes require reauthentication. Frontend guards use server-returned permissions; named server policies remain authoritative.

The notification badge uses `GET /api/notifications/unread-count` and does not depend on Workwear dashboard access. Existing API routes remain compatible, with effective `permissions` added to auth/user responses. The changes require no database migration.

Local Vite requests use `/api` and proxy to the existing backend launch profile at `http://localhost:5188`; set `API_PROXY_TARGET` when launching Vite to use another target. For a separate production API, set `VITE_API_BASE_URL` at build time and configure exact API CORS origins; alternatively use a same-origin HTTPS reverse proxy for `/api`. `frontend/.env.example` documents the default. The frontend requires the API to be running with migrations applied and an explicitly configured administrator; there is no demo sign-in fallback.

Verification: frontend typecheck/build and eight transport/navigation tests pass. Tests cover shared rotating refresh, revoked sessions, late responses after sign-out, no replay on 403/409, multipart boundaries, validation details, and permission-filtered navigation. Backend role-policy tests cover all three roles and unknown future-module roles; 41 tests pass with seven opt-in SQL Server tests skipped in the default run. EF reports no pending model changes. Browser interaction against the running local API and SQL Server remains an explicit acceptance check; the automated transport tests are not a substitute for that check. CI remains the next delivery item.

Frontend file restructuring is complete. `App.tsx` composes sign-in, navigation, the application layout, and the active page. Extracted pages own their forms, filters, and upload steps; dashboard-only cards/charts remain in `DashboardPage.tsx`. Shared presentation components live in `components/`, navigation and titles in `app/navigation.ts`, and layout/sidebar/header in `layouts/`. Temporary demo contracts live in `types/demo.ts`; seed data, business rules, and state mutations are isolated in `demo/`. The ISO date helper lives in `utils/dates.ts`. The subsequent API integration is complete: the session provider, typed services/contracts, and TanStack Query replace demo data and browser credentials.

Frontend restructuring verification: `npm.cmd run typecheck` and `npm.cmd run build` pass. A one-time render comparison verified identical HTML for all nine extracted screens/forms, sidebar, header, status badge, toggle, and logo against the original application. Existing demo behavior is preserved, including transient in-memory updates and the static import preview. Vite still reports its existing configuration warning about native-loader compatibility.

Dashboard and employee imports are implemented in the API. Apply `AddDashboardReportingAndEmployeeImports` with `dotnet ef database update --project backend` before using the new endpoints. This additive migration stores import batches/rows and adds the monthly ledger query index; it preserves existing employee and inventory data. Frontend API integration is complete; live browser acceptance and CI are next.

`GET /api/dashboard?year=2026` returns active/inactive employee counts, counts for all four request statuses, total persisted inventory balance, active catalog items at or below the low-stock threshold, and the current user's unread notification count. Low-stock item counts remain visible even when alert generation is disabled. The report defaults to the current business year and accepts years 1900–9998. Twelve monthly issue buckets, including zero months, count allocation ledger entries using UTC ranges derived from the configured business time zone. Receipts and adjustments do not count as issues. A serializable read transaction keeps the response consistent.

Administrator-only import endpoints are `POST /api/employee-imports` (multipart field `file`), `GET /api/employee-imports` (history), `GET /api/employee-imports/{id}` (preview), and `POST /api/employee-imports/{id}/commit`. History defaults to 25 batches per page; previews default to 100 rows per page; both accept `page`/`pageSize` with a maximum of 100. Upload responses contain the batch summary and a preview Location. Summaries include original file name, size, SHA-256 fingerprint, uploader/time, status, row/error counts, imported count, and commit actor/time. Error count means rows containing errors. Normalized rows, validation errors, and resulting employee IDs persist in SQL Server and survive restarts. No mock records are seeded.

Imports accept UTF-8 comma-separated CSV, XLSX, and legacy XLS up to 10 MB and 10,000 nonblank employee rows. Workbooks must contain exactly one worksheet. Required, case-insensitive headers are `Employee ID`, `Full Name`, `Department`, and `Enrollment Date`; `Job Title` and `Notes` are optional. CSV supports quoted commas, escaped quotes, and multiline fields. Dates must be ISO `yyyy-MM-dd` strings or date-formatted Excel cells; ExcelDataReader handles both 1900 and 1904 workbook date systems. Unformatted Excel serial numbers are rejected as ambiguous. Employee numbers are trimmed/uppercased; department names or codes must match an active reference record. Validation covers required fields, employee field lengths, duplicate numbers within the file and existing active/inactive records, and the applicable department/season rule at the first six-month anniversary. Malformed files or headers return HTTP 400; row-level errors create an `Invalid` batch with a reviewable preview.

Commit uses the existing organization workflow lock and a serializable transaction, revalidates stored rows against current departments/rules and employee numbers, and saves all employee records, row outcomes, redacted employee audits, and the batch audit atomically. Invalid batches return HTTP 409 and require a corrected upload. A batch invalidated by changes after preview persists updated errors and a rejection audit. Concurrent or repeated commits of the same batch return the original committed summary; they never create duplicate employees or audit events. Reuploading an already imported file previews duplicate employee errors. Parse-time uploads are held only in memory and discarded after staging; no original uploaded files are retained or exposed through static hosting. XLSX expanded packages are limited to 100 MB. Virus scanning and retention periods remain deployment infrastructure/policy decisions.

Dashboard/import verification: all 45 backend tests pass, including seven SQL Server tests in isolated local `SQLEXPRESS` databases. New coverage checks CSV previews, required fields and duplicates, Excel dates in both date systems, authorization, malformed/oversized files, post-preview revalidation, import rollback/retry, concurrent idempotent commit, persisted import history/audits, and dashboard business-month boundaries and per-user notification counts. Run `$env:HR_ETRACKER_SQL_TESTS = '1'` then `dotnet test backend/HrETracker.slnx --configuration Release`; without the variable, 38 tests pass and seven SQL Server tests are skipped. EF reports no pending model changes. Example requests are in `backend/HrETracker.http`.

Inventory and request workflows are implemented in the API. Apply the `CompleteInventoryAndRequestWorkflows` migration with `dotnet ef database update --project backend` before starting the API. It preserves existing balances and receipts, initializes missing catalog balances to zero, and seeds department/season rules and organization settings. No employee, request, stock receipt, or notification mock data is seeded. Frontend API wiring is now complete.

Inventory exposes catalog quantities and opaque balance row versions, paginated movement history, positive receipts, and signed adjustments. Adjustments require a reason and the latest balance `rowVersion`; negative balances and quantity overflow are rejected. Receipts accept optional `reference` and `note`. The stock ledger is append-only through the application. `GET/POST /api/inventory/item-rules` and `PUT /api/inventory/item-rules/{id}` manage effective-dated rules; updates require `rowVersion`. Rules for the same department/season cannot overlap, and a matching seasonal rule takes precedence over an all-season rule. Retiring a rule does not alter the item saved on an existing request.

The daily worker checks the configured business date every five minutes, runs once per successful day, and retries failures. Startup and protected manual runs catch up every missing six-month anniversary, including month-end anniversaries. Requests support status/department filters, paging, detail, provide, and administrator cancellation. Provide/cancel require `{ rowVersion, notes? }`. A missing or ambiguous rule creates an unassigned outstanding request and configuration notification; an administrator can resolve it through `POST /api/coat-requests/{id}/assign-item` with `{ inventoryItemId, rowVersion, notes? }`. Cancelled and provided cycles never regenerate. Inactive employees cannot receive coats.

Stock workflows use an explicit serializable transaction and a transaction-owned SQL Server application lock, shared across API instances and worker runs. Provision also uses a guarded decrement requiring at least one unit. Balance, allocation, completion, provider, audit event, and notification commit together; stale writes or unavailable stock return problem-details HTTP 409. The organization-wide lock favors simple correctness at this application's scale; larger workloads may later partition locks by item. Replenishment makes outstanding requests pending again, and depletion marks them out of stock.

`GET/PUT /api/settings` persists the business time zone, low-stock preference, and threshold; updates require `rowVersion`. Low-stock means quantity at or below the threshold. One unresolved alert per item remains open regardless of read state, resolves when stock recovers or alerts are disabled, and can recur after a later depletion. Settings changes reevaluate catalog items immediately. `GET /api/notifications`, `POST /api/notifications/{id}/read`, and `POST /api/notifications/read-all` persist read state separately for each user.

Workflow verification: all 35 backend tests pass, including five SQL Server integration tests for upgrade migrations preserving existing stock, simultaneous due-cycle generation/receipts, concurrent provision of one request and competing requests for one unit, and rollback after an injected allocation failure. The SQL Server tests create and remove isolated databases on local `SQLEXPRESS`; opt in with `$env:HR_ETRACKER_SQL_TESTS = '1'` before `dotnet test backend/HrETracker.slnx --configuration Release`. The default suite uses SQLite and skips the SQL Server tests. EF model validation reports no pending changes.


Employee management is implemented with `GET /api/departments` and the employee list/detail/create/update/deactivate endpoints. Lists return `{ items, totalCount, page, pageSize }`, support employee number/name search, `departmentId`, and `status=active|inactive|all` (active by default), and limit page size to 100. Create/update use department IDs; updates cannot change employee numbers. Employee numbers are trimmed and normalized to uppercase, remain unique across inactive records, and rehired employees need a new record and employee number. Deactivation requires a row version; stale updates return problem-details HTTP 409. Employee writes save actor/timestamp fields and redacted, append-only audit events in the same transaction. Six departments are seeded; employee data starts empty. Apply the `AddDepartmentsAndEmployeeLifecycle` migration before running the API. Frontend API wiring is now complete.

Employee verification: `dotnet test backend/HrETracker.slnx --configuration Release` passes all 18 authentication and employee integration tests. EF reports no pending model changes. An isolated local SQL Server database also passed migration, rowversion/stale-write, and rollback checks and was removed after verification. Apply migrations locally with `dotnet ef database update --project backend`.

Authentication and browser integration are complete; live browser acceptance remains pending. Access tokens last 15 minutes by default. Refresh tokens rotate on every refresh, have an absolute seven-day session lifetime, and are stored only as SHA-256 hashes. Clients send bearer access tokens in the `Authorization` header and refresh via an HttpOnly cookie at `/api/auth/refresh`, with no request body and X-HR-Auth: 1. The frontend automatically restores sessions on reload; access tokens remain in memory. Logout revokes the current session immediately. Password changes and user updates invalidate all existing sessions through the Identity security stamp. Five failed password attempts lock the account for 15 minutes; authentication endpoints also allow at most 20 requests per IP per minute. The last active administrator cannot be deactivated or lose the administrator role.

Configure `Authentication__Jwt__SigningKey` (at least 32 bytes), `InitialAdmin__Email`, and `InitialAdmin__Password` through environment variables, user secrets, or ignored `backend/.env`. The bootstrap password is required only when creating the account. Successful bootstrap persists `Exists: true` in ignored `backend/identity-bootstrap.json` and skips future role/admin checks; delete that file after resetting the database or deleting admin/role records. Existing accounts and passwords are preserved. Apply EF migrations before starting the API. Swagger supports bearer authorization in development.

Authentication verification: `dotnet test backend/HrETracker.slnx --configuration Release`. The API integration tests use an isolated SQLite database; SQL Server migration/model validation is checked separately. Wider SQL Server concurrency tests and CI remain in the testing delivery item.

| Decision | Chosen approach |
| --- | --- |
| Sign-in | Local ASP.NET Core Identity accounts. Users sign in with email and password; roles are maintained in the application. |
| SQL Server | Local `SQLEXPRESS` instance with database name `HrETracker` for development. |
| Business time zone | `Africa/Casablanca` (Morocco). Store this IANA identifier in organization settings and use calendar-month six-month anniversaries. |
| Employee import format | UTF-8 CSV, `.xlsx`, and legacy `.xls`. Use the permissively licensed, open-source [ExcelDataReader](https://github.com/ExcelDataReader/ExcelDataReader) library for spreadsheet parsing. |
| Inventory scope | Track only the seven coat variants present in the demo. Sizes, locations, supplier purchase orders, and reservations are deferred. |
| Rehire policy | Deactivate employees on departure. A returning employee receives a new employee record and a new enrollment date, so their coat cycle restarts. |

## Remaining policy decision

- Confirm the retention period for employee details, uploaded import files, audit records, and notifications before production deployment. The implementation can proceed with a configurable retention policy and a conservative default.

