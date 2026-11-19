# Application architecture

The MVC application uses three logical layers in the existing web project:

`Controller -> BLL service interface -> DAL repository interface -> EF Core / Identity -> SQL Server`

## Responsibilities

- **Controllers/**: authorization attributes, request binding, binding validation, current user ID, HTTP status codes, TempData, redirects and view selection. Controllers do not query the database.
- **BLL/**: business validation, role rules, partial profile updates, weekly schedule composition, working-hour rules and break overlap checks. Services return models or `OperationResult`, never MVC results. Services depend on repository interfaces, not DbContext or IQueryable.
- **DAL/**: materialized EF queries, persistence, Identity user/role access, transaction implementation, database context and seeders. Repositories do not select views or return HTTP responses. Tracked entities loaded for updates are saved within the same scoped DbContext.
- **Models/**: entity classes grouped into Appointment, Consultant and Customer; Home contains the existing error view model.
- **ViewModels/**: Admin and Consultant request/response models, grouped by feature. These are shared data contracts for MVC and BLL.
- **Views/**: existing Admin, Consultant, Customer and Home folders. Their locations and existing URLs are unchanged.

## Feature folders

Each controller lives in its own feature folder. Admin and Consultant have BLL services and DAL repositories. Home has a BLL service for dashboard role precedence and no database dependency. Customer has a BLL service for slot generation and booking rules, plus a DAL repository for consultant schedules, customer appointments and atomic booking writes.

`DependencyInjection.cs` registers services and repositories as scoped dependencies. `Program.cs` is the composition root and calls `AddApplicationLayers()`.

## Persistence and compatibility

Entity and view-model namespaces remain stable after moving their files. The context and seeders now live under DAL but retain their original namespace so migrations and Identity configuration remain compatible. Existing migrations are unchanged; no migration or data repair is required for this refactor.

Admin updates pass the submitted field names to BLL separately from their values. Missing fields preserve saved values; explicit blank required values fail validation. Profile and Identity role writes share one DAL transaction; role failures roll it back.

Consultant queries always scope records to the authenticated user's profile. Schedule changes and break additions retain the existing rules, including deletion of recurring breaks on removed working days. The dashboard uses Bangladesh time and a Monday-Sunday week. Missing consultant profiles still require setup; the refactor does not create profile data automatically.

Scaffolded Identity account Razor Pages continue to use ASP.NET Core Identity directly. They are authentication framework endpoints, outside the four MVC controller features refactored here.

## Verification

From the repository root, when the app is stopped:

```powershell
dotnet run --project tests/ArchitectureChecks/ArchitectureChecks.csproj
```

When the development app is running and locks its output:

```powershell
dotnet build tests/ArchitectureChecks/ArchitectureChecks.csproj -o "$env:TEMP/AppointmentArchitectureChecks" -p:UseAppHost=false
dotnet "$env:TEMP/AppointmentArchitectureChecks/ArchitectureChecks.dll"
```

The dependency-free regression harness exercises service rules with fake DAL implementations, transaction outcomes, dependency injection, MVC action discovery and compiled view discovery. It does not modify a database. Real SQL transaction behavior and authenticated browser flows should also be smoke-tested before deployment.

## Customer booking

`CustomerController -> ICustomerService -> ICustomerRepository` uses the existing Customers, Consultants, ConsultantAvailabilities, ConsultantBreaks and Appointments tables. No schema changes or persisted slot table are required.

The page defaults to today in Bangladesh time and offers date selection. Each active consultant shows weekly working hours and selected-date slots. Slots start at the working day's start time and use the consultant's appointment duration; incomplete trailing slots are omitted. Overlaps with weekly/date-specific breaks, non-cancelled appointments, or the customer's other appointments are unavailable. Past slots cannot be booked. Other customers' identities and appointment details are never sent to the view.

Booking takes only consultant ID, date and start time. Customer identity comes from the authenticated claim, and end time/status are determined in BLL. A completed customer profile is required. The DAL starts a SQL Server serializable transaction before BLL re-reads working hours, breaks and appointments, validates the requested slot and inserts the appointment. Range locks and the existing unique index prevent competing bookings from both committing. Deadlocks, duplicate keys and lock timeouts during availability reads/writes are returned as a retry message. The transaction is disposed without commit on validation failure. Consultant schedule/break editing behavior is unchanged.

The customer page shows the customer's own appointments for the selected date after booking. Cancellation and rescheduling are outside this feature. A TimeProvider dependency allows deterministic testing of same-day cutoffs.

The regression harness now includes customer slot rules and real local HTTP requests through MVC/Razor, authentication and antiforgery, using a fake DAL and test-only authentication handler. Production DI never registers that handler. These checks do not exercise SQL Server concurrency against a live database; run that integration check before deployment.
