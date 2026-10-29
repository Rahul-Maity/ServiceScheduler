# Domain layer

Core entities and scheduling rules. **No references** to other projects, and no ASP.NET Core / EF Core / Npgsql types.

## Contents
- `Entities/`: Appointment, Technician, ServiceBay, ServiceType, Vehicle, Customer, Dealership, WorkingHours (plus skill/capability links).
- `Enums/`: AppointmentStatus (at least Confirmed, Cancelled).
- `Interfaces/`: repository and Unit of Work contracts (implemented in Infrastructure).
- `Exceptions/`: domain exceptions (validation failure, resource unavailable, invalid state transition).

## Rules to encode here
- `EndAt = StartAt + ServiceType.Duration`; times are UTC (`DateTimeOffset` with zero offset, or UTC `DateTime`).
- Half-open interval `[StartAt, EndAt)`: touching intervals do not overlap.
- Technician has the required skills; bay has the required capabilities; both belong to the dealership; the interval fits dealership working hours.
- Valid state transitions only (e.g. Confirmed -> Cancelled; no re-cancelling).
- Only Confirmed appointments reserve resources.
- Keep entities consistent through constructors/methods, not public setters everywhere.
