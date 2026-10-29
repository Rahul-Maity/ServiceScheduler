# Infrastructure layer

EF Core + PostgreSQL (Npgsql). References Application and Domain. Implements their interfaces.

## Contents
- `Persistence/`: DbContext, `Configurations/` (IEntityTypeConfiguration per entity), `Migrations/`.
- `Repositories/`: appointment, customer, technician, service bay, etc. + Unit of Work/transaction handling.
- `Services/`: idempotency store, and other implementations of Application interfaces.
- DI registration via an `AddInfrastructure(...)` extension method called from Api.

## Database integrity (source of truth for concurrency)
- Store all timestamps as `timestamptz`, UTC.
- Add FKs, indexes, and a unique constraint on the idempotency key.
- **Exclusion constraints** via raw SQL in migrations (EF can't model them): enable `btree_gist`, then for Confirmed appointments
  `EXCLUDE USING gist (technician_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&) WHERE (status = 'Confirmed')`, and the same for `service_bay_id`. Use the `[)` bound to match the half-open rule.
- Translate `PostgresException` SQLSTATE `23P01` (exclusion violation) and `23505` (unique violation) into application/domain conflict errors. Nothing above this layer should see Npgsql types.
- Appointment + idempotency record + cancellation each commit in a single transaction.

## Notes
- Migration command: `dotnet ef migrations add <Name> --project ServiceScheduler.Infrastructure --startup-project ServiceScheduler.Api --output-dir Persistence/Migrations`.
- Connection string comes from Api configuration, never hard-coded.
- Health check for `/health/ready` verifies DB connectivity.
