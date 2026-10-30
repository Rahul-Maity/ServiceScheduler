# ServiceScheduler

A backend API for booking vehicle service appointments. An appointment is confirmed only when a qualified technician and a compatible service bay are both free for the entire service duration.

Stack: .NET 10, ASP.NET Core Web API, EF Core, Npgsql, PostgreSQL, Swagger.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 17 running locally on `localhost:5432`
- EF Core tools:

```bash
dotnet tool install --global dotnet-ef
```

## 1. Create the database

Create an empty database named `servicescheduler` in PostgreSQL. The migrations create the tables, the `btree_gist` extension, and the overlap constraints.

## 2. Configure the connection string

The connection string is read from `ConnectionStrings:Scheduler`. Create `ServiceScheduler.Api/appsettings.Development.json` with your own PostgreSQL password:

```json
{
  "ConnectionStrings": {
    "Scheduler": "Host=localhost;Port=5432;Database=servicescheduler;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

This file is listed in `.gitignore` and must not be committed.

## 3. Apply the migrations

The design-time tools read the connection string from an environment variable. Set it for the current terminal, then run the update:

```powershell
$env:ConnectionStrings__Scheduler = "Host=localhost;Database=servicescheduler;Username=postgres;Password=YOUR_PASSWORD"
dotnet ef database update --project ServiceScheduler.Infrastructure --startup-project ServiceScheduler.Api
```

This loads the demo seed data (`SeedDemoData`) as well.

## 4. Run the API

```bash
dotnet run --project ServiceScheduler.Api --launch-profile http
```

The API listens on `http://localhost:5020`. Open Swagger at:

```
http://localhost:5020/swagger
```

## Demo data

The seed migration adds two dealerships, both open 08:00 to 18:00 every day (`Asia/Kolkata`):

| Item | ID |
|---|---|
| Dealership, Mumbai | `11111111-1111-1111-1111-111111111111` |
| Dealership, Delhi | `12121212-1212-1212-1212-121212121212` |
| Customer One | `33333333-3333-3333-3333-333333333333` |
| Customer Two | `34343434-3434-3434-3434-343434343434` |
| Vehicle, Honda Civic | `44444444-4444-4444-4444-444444444444` |
| Service type, Brake service (1 hour) | `22222222-2222-2222-2222-222222222222` |
| Service type, Engine diagnostic (2 hours) | `23232323-2323-2323-2323-232323232323` |
| Service type, Wheel alignment (45 minutes) | `24242424-2424-2424-2424-242424242424` |

Mumbai has two technicians and two bays. Delhi has the same. See `SeedDemoData.cs` for the full list.

## Endpoints

Timestamps must include a UTC offset, for example `2026-11-02T10:00:00+05:30`. Requests without an offset are rejected.

### 1. Check availability (reserves nothing)

`POST /api/availability`

```json
{
  "dealershipId": "11111111-1111-1111-1111-111111111111",
  "serviceTypeId": "22222222-2222-2222-2222-222222222222",
  "startAt": "2026-11-02T10:00:00+05:30"
}
```

Response: `isAvailable`, `startAt`, and `endAt` in UTC.

### 2. Book an appointment

`POST /api/appointments`

Required header: `Idempotency-Key: <any unique value>`

```json
{
  "customerId": "33333333-3333-3333-3333-333333333333",
  "vehicleId": "44444444-4444-4444-4444-444444444444",
  "dealershipId": "11111111-1111-1111-1111-111111111111",
  "serviceTypeId": "22222222-2222-2222-2222-222222222222",
  "startAt": "2026-11-02T10:00:00+05:30"
}
```

- `201 Created`: the appointment is confirmed.
- `200 OK`: the same key and the same body were sent again. The original appointment is returned with a message. No duplicate is created.
- `409 Conflict`: no technician and bay are free for that time, or the key was reused with a different body.
- `400`/`404`/`422`: validation or not-found errors, returned as ProblemDetails.

### 3. Get an appointment

`GET /api/appointments/{id}`

### 4. Cancel an appointment

`POST /api/appointments/{id}/cancel`

Required header: `Idempotency-Key: <any unique value>`

Cancelling releases the technician and bay, so the slot can be booked again.

## Testing the flow in Swagger

1. Call `POST /api/availability` with a time inside opening hours. Expect `isAvailable: true`.
2. Call `POST /api/appointments` with a new `Idempotency-Key`. Expect `201` and an `id`.
3. Repeat step 2 with the same key and body. Expect `200` with the same `id`.
4. Book the same time again with a new key. Expect `201` with a second technician and bay, until none are free.
5. Call `GET /api/appointments/{id}` with the `id` from step 2.
6. Call `POST /api/appointments/{id}/cancel` with a new key.
7. Book the same slot again. Expect `201`, because the cancellation released it.

## Running the tests

```bash
dotnet test
```

Stop the running API first. Otherwise the build cannot overwrite the DLLs it is using.
