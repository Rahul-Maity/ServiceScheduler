# Api layer

ASP.NET Core Web API entry point. References Application and Infrastructure. Thin: no business logic.

## Contents
- `Controllers/`: validate input, call application services, return status codes. `201` created, `409` conflict, `400/422` validation, `404` not found.
- `Middleware/`: exception handling -> ProblemDetails, request logging, correlation ID, `Idempotency-Key` header extraction. (Auth/rate limiting possible later.)
- `Contracts/`: HTTP request/response models, separate from Application DTOs.
- `Program.cs`: DI composition, Swagger/OpenAPI (Swashbuckle), health checks `/health/live` and `/health/ready`, OpenTelemetry setup (later phase).

## Rules
- Accept timestamps with a UTC offset; reject ones without. Pass through to Application, which normalizes to UTC.
- Booking and cancellation require an `Idempotency-Key` header.
- The template also ships `Microsoft.AspNetCore.OpenApi`; avoid double OpenAPI registration when wiring Swashbuckle.
- Swagger UI is the primary test client (Development environment).
