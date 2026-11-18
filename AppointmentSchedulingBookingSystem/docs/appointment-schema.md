# Appointment schema

Apply migrations before starting the app:

```powershell
dotnet ef database update -- --IdentitySeed:Enabled=false
dotnet run --environment Development
```

Development startup with `IdentitySeed:Enabled=true` creates missing customer and consultant profiles. A new sample consultant has Saturday 09:00-17:00 and Sunday 10:00-14:00 availability, 30-minute slots, and a Saturday 13:00-14:00 break. Existing profiles and schedules are preserved. No appointments are seeded.

## Rules

- Consultant email comes from the linked Identity user; UserId is unique.
- SlotDurationMinutes belongs to the consultant and is the default for their weekly schedule.
- One working window per consultant per weekday. DayOfWeek follows .NET: Sunday=0 through Saturday=6.
- Dates and times are clinic-local wall-clock values; the booking feature must choose the clinic timezone consistently when checking past slots. Audit timestamps are UTC.
- Working windows, breaks, and appointments must end after they start on the same day. Overnight windows are not supported.
- Each break specifies exactly one of DayOfWeek (weekly) or Date (one-off). A full working-day break can represent leave.
- Appointments store actual start/end times, so later slot-duration changes do not alter historical bookings.
- Status values: Confirmed=0, Cancelled=1, Completed=2, NoShow=3.
- A filtered unique index rejects duplicate non-cancelled start times for the same consultant/date. Cancellation releases that start time.
- This index does not prevent overlaps with different start times. The booking feature must validate working hours, breaks, duration, past dates, and overlaps inside concurrency-safe booking logic.
- Appointment foreign keys prevent deleting customer/consultant profiles with history. Deleting an unused consultant removes their availability and breaks. Deactivate consultants to preserve their profiles.
- EF SaveChanges maintains appointment UTC audit timestamps. Direct SQL and bulk updates must maintain timestamps explicitly.

## Verification

Run `scripts/verify-appointment-schema.sql` in SQL Server Management Studio against the migrated, seeded development database. It checks constraints, foreign keys, cancellation reuse, dated breaks, and deletion protection inside a rolled-back transaction. It leaves no test records (identity counters may advance).

For an active app that locks the normal build output, use `--configuration SchemaCheck` with dotnet build/ef commands.
