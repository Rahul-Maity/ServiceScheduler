# Application layer

Coordinates the booking workflow. References **Domain only**. Never reference Infrastructure or Api; depend on Domain/Application interfaces.

## Contents
- `Services/`
  - **AvailabilityService**: checks qualified technician + compatible bay for the whole duration. Returns a result and **reserves nothing**.
  - **BookingService**: creates the appointment, coordinates the transaction, handles conflicts and duplicate requests; cancellation.
  - **Resource selection**: picks a technician and bay (skills, capabilities, dealership, working hours, no overlap). Should be able to return alternative combinations for retry.
  - **Policy coordination**: invokes domain rules; does not define them.
- `Interfaces/`: service contracts and abstractions Infrastructure implements (e.g. idempotency store, clock).
- `DTOs/`: request/response models used between Api and services.
- `Validators/`: input validation (required fields, offset present, start time valid).

## Booking flow
1. Check idempotency key: same key + same request hash returns the stored result; same key + different hash is an error.
2. Load service type (duration, required skills/capabilities) and working hours; compute UTC `[StartAt, EndAt)`.
3. Select candidates, re-check availability inside the transaction.
4. Save appointment + idempotency record together (one Unit of Work commit).
5. On a DB conflict surfaced as a domain/application error, roll back and try the next candidate pair; when none remain, report a conflict (Api maps to 409).

Do not catch EF/Npgsql exceptions here; Infrastructure translates them first.
