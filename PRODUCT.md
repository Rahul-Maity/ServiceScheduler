# Product Overview: Unified Service Scheduler

## The problem

Vehicle dealerships still book many service appointments by hand: phone calls, spreadsheets, paper diaries. This causes real problems:

- **Double bookings.** Two staff members promise the same technician or the same bay at the same time.
- **Wrong fit.** A job gets booked with a technician who lacks the right skills, or in a bay that lacks the right equipment (for example, a lift for a tyre job or an alignment rig).
- **Wasted time.** Staff spend time checking who is free and calling customers back to reschedule.
- **Unhappy customers.** Appointments get moved or cancelled at the last minute because the dealership's availability wasn't accurately known.

A service appointment is only possible when **two things are free at once**: a qualified technician *and* a suitable service bay, for the full length of the job. Doing this by hand is slow and error-prone.

## What we are building

A backend system that lets a customer, or any app acting for them, request a service appointment and get an instant, reliable answer:

> "I want service X for my vehicle Y at dealership Z, starting at this time."

The system:

1. Works out how long that service takes.
2. Finds a qualified technician and a compatible bay that are both free for that whole period.
3. Confirms the booking and saves a clear record of who, what, where and when.
4. Refuses politely if nothing is available, instead of over-promising.

The aim is to **replace manual booking with a system that is trustworthy**: if it says an appointment is confirmed, it really is.

## Who uses it

- **Customers** booking or cancelling a service for their vehicle.
- **Dealership staff** relying on the schedule being accurate.
- **Client applications** (a website, mobile app or call-centre tool) that call our API. We don't build a front end; Swagger/cURL is enough to try it out.

## What a customer can do

- **Check availability** for a service at a dealership and time, without committing to anything.
- **Book an appointment** for a specific vehicle, service type, dealership and start time.
- **View an appointment** and see its details: customer, vehicle, technician, bay, time.
- **Cancel an appointment**, which frees the technician and bay for someone else.

## Promises the product makes

- **No double bookings.** The same technician or bay can never be booked twice for overlapping times, even if many people book at the same moment.
- **Right person, right place.** Only technicians with the needed skills and bays with the needed equipment are used.
- **Back-to-back is fine.** A job ending at 12:00 lets the next one start at 12:00.
- **Time zones just work.** Customers can send times in their local time zone; everything is stored and compared consistently.
- **Looking isn't booking.** Checking availability never holds a slot. Availability is checked again at the moment of booking.
- **Safe retries.** If a customer's app retries after a network blip, they get the same appointment, not a duplicate.
- **Clean cancellations.** Cancelling releases the slot immediately and reliably.
- **Clear answers.** Success is a confirmation; if it can't be done, the customer gets a clear "this slot is not available" message.

## Scope

**In scope:** booking, availability check, viewing and cancelling appointments, resource matching (skills and bay capabilities), dealership working hours, protection against double booking and duplicate requests, basic monitoring and health checks.

**Out of scope (for now):** a user-facing front end, payments, notifications (email/SMS), rescheduling flows, multi-dealership optimisation, and reporting dashboards. Authentication and rate limiting may come later.

## How we'll know it works

- Two people booking the same slot at the same time result in exactly one confirmed appointment.
- Every confirmed appointment has a customer, vehicle, technician and bay that genuinely fit the job.
- Retrying a request never creates a second appointment.
- A cancelled slot can be booked again straight away.
- The team can see how bookings are performing and diagnose failures through logs, metrics and health checks.

## Where to go next

Technical rules, architecture and constraints live in [CLAUDE.md](CLAUDE.md) and the per-layer `CLAUDE.md` files.
