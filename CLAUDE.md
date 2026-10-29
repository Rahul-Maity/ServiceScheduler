# ServiceScheduler

Backend for booking vehicle service appointments (Keyloop Scenario A: "The Unified Service Scheduler"). It replaces manual booking. An appointment is confirmed only when a **qualified technician AND a compatible service bay** are both free for the **entire** service duration.

The hard part is concurrency: two requests can see the same free slot at once. The answer is a **database-level no-overlap rule** plus an **atomic** reservation + appointment creation. Do not rely on application-level checks alone.

## Status

- Done: solution scaffold, 4 projects, references, folder structure, NuGet packages (EF Core, Npgsql, Swashbuckle).
- Not done (build incrementally, one phase at a time, only when asked): domain entities, repositories, migrations, endpoints, idempotency, observability, tests.
- Don't implement ahead of the requested phase. Don't claim a build or test passed unless it was actually run.

## Stack

C# / .NET 10 LTS, ASP.NET Core Web API, EF Core + Npgsql (PostgreSQL), Swagger/OpenAPI (Swashbuckle). Planned: xUnit + Testcontainers for .NET (real PostgreSQL), OpenTelemetry. Keep package versions compatible with .NET 10 and each other. No microservices and no extra abstractions beyond what is below.

## Solution layout and dependency rules

```
ServiceScheduler.Api            -> Application, Infrastructure   (controllers, middleware, contracts, Program.cs)
ServiceScheduler.Application    -> Domain                        (services, interfaces, DTOs, validators)
ServiceScheduler.Domain         -> nothing                       (entities, enums, interfaces, exceptions)
ServiceScheduler.Infrastructure -> Application, Domain           (EF Core, repositories, migrations, services)
```

Hard rules:
- Domain references nothing. Application must not reference Infrastructure or Api.
- Business rules stay independent of ASP.NET Core, EF Core and PostgreSQL.
- Domain defines repository/UoW interfaces; Infrastructure implements them; Api wires DI.
- The solution file is `ServiceScheduler.slnx` (.NET 10 default format).

Each project has its own `CLAUDE.md` with layer-specific guidance.

## Core business rules

1. A request has: customer, vehicle, dealership, service type, desired start time.
2. Each service type has a configured duration. `EndAt = StartAt + Duration`, computed by the backend.
3. Technician must hold the required skill(s). Bay must support the required capability/capabilities. Both must belong to the requested dealership and fall within dealership working hours.
4. Intervals are **half-open `[StartAt, EndAt)`**. An appointment ending at 12:00 and another starting at 12:00 do not overlap.
5. API accepts timestamps **with a UTC offset** (e.g. `10:00 +05:30`). Reject timestamps without an offset. Normalize to **UTC** for storage and comparison.
6. **Checking availability never reserves anything.**
7. Booking **re-checks** availability, because a resource may be taken after the earlier query.
8. Only **confirmed** appointments reserve a technician or bay. Cancelled ones do not.
9. Appointment creation and resource reservation are **one atomic transaction**.
10. The DB must reject overlapping active appointments for the same technician or the same bay, including under concurrent requests.
11. Cancellation releases the reservation and persists the cancellation in a **single transaction**; valid state transitions are enforced in the domain.
12. Repeating a booking with the same idempotency key and same request must not create a duplicate.

## Concurrency design (the core of the system)

- PostgreSQL **exclusion constraints** (`EXCLUDE USING gist`, needs the `btree_gist` extension) on `(technician_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&)` and the same for `service_bay_id`, scoped to active/confirmed appointments (partial constraint, `WHERE status = 'Confirmed'`). EF Core can't express this natively, so use raw SQL in migrations.
- Flow: validate -> idempotency lookup -> find candidate technician + bay -> insert appointment and idempotency record in **one transaction** -> on exclusion-violation (SQLSTATE `23P01`), roll back and **try another suitable resource combination**; if none remain, return 409.
- Translate known DB conflicts into application errors in Infrastructure; do not leak `PostgresException` upward.

## Idempotency

- Booking **and cancellation** endpoints take an `Idempotency-Key` header (extracted in middleware).
- Store: key, request hash, and the successful response. Same key + same hash -> return the original result. Same key + different hash -> 409/422 conflict error.
- Appointment and idempotency record are saved together in one transaction. Unique constraint on the key.

## API behavior

- `POST` availability check -> returns availability, reserves nothing.
- `POST` booking -> `201 Created` on success, `409 Conflict` when it cannot proceed (no resource available / race lost), `400/422` for validation errors.
- Retrieve appointment by id. Cancel appointment.
- Consistent error responses (ProblemDetails) from exception-handling middleware. Swagger UI is the test client; no separate frontend.
- If the client disconnects mid-flow, honor cancellation where possible; an already-**confirmed** booking stays confirmed.

## Cross-cutting (middleware / observability)

Middleware: error handling, request logging, correlation IDs, idempotency-header extraction (auth and rate limiting are listed as possible later additions).
Observability via OpenTelemetry: structured logs (booking success, conflicts, transaction failures, duplicates, with correlation IDs, **no sensitive customer data**), metrics (latency, error rate, booking success/conflict rate, DB failures), traces (API -> services -> DB). Health checks: `/health/live` (process up) and `/health/ready` (includes PostgreSQL). Observability must never be a dependency for a successful booking.

## Testing plan (introduce in a later phase)

xUnit for domain/application rules. Testcontainers + real PostgreSQL for integration tests. Must cover:
- Availability: qualified tech + compatible bay + working hours; unqualified tech and incompatible bay rejected.
- Half-open boundaries (back-to-back is allowed, 1-minute overlap is not).
- UTC normalization of offset timestamps.
- **Concurrent bookings for the same slot** -> exactly one succeeds, the rest get 409.
- Fallback to another technician/bay on conflict.
- Idempotent retry returns the same appointment; different payload with the same key is rejected.
- Cancellation releases resources atomically; the slot becomes bookable again.
- Transaction rollback leaves no partial state.

## Working conventions

- Work incrementally; follow the step the user asks for and stop there.
- Prefer simple, maintainable defaults. Match existing code style; no speculative abstractions.
- Explain any deviation from the specified stack or commands before applying it.
- The design treats AI output as draft proposals: verify against these requirements and reject added complexity.

## Commands

```
dotnet restore
dotnet build
dotnet run --project ServiceScheduler.Api
dotnet ef migrations add <Name> --project ServiceScheduler.Infrastructure --startup-project ServiceScheduler.Api --output-dir Persistence/Migrations
dotnet test
```
