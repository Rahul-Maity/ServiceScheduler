using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments", t =>
            t.HasCheckConstraint("ck_appointments_end_after_start", "end_at > start_at"));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.CustomerId).HasColumnName("customer_id");
        builder.Property(a => a.VehicleId).HasColumnName("vehicle_id");
        builder.Property(a => a.DealershipId).HasColumnName("dealership_id");
        builder.Property(a => a.ServiceTypeId).HasColumnName("service_type_id");
        builder.Property(a => a.TechnicianId).HasColumnName("technician_id");
        builder.Property(a => a.ServiceBayId).HasColumnName("service_bay_id");
        builder.Property(a => a.StartAt).HasColumnName("start_at").HasColumnType("timestamptz");
        builder.Property(a => a.EndAt).HasColumnName("end_at").HasColumnType("timestamptz");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        builder.Property(a => a.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamptz");
        // Stored as text ('Confirmed'/'Cancelled'): the exclusion constraints filter on this value.
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Ignore(a => a.ReservesResources);

        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Dealership>().WithMany().HasForeignKey(a => a.DealershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceType>().WithMany().HasForeignKey(a => a.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Technician>().WithMany().HasForeignKey(a => a.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceBay>().WithMany().HasForeignKey(a => a.ServiceBayId).OnDelete(DeleteBehavior.Restrict);

        // The no-overlap exclusion constraints (technician and bay) live in the migration as raw SQL.
        // The gist indexes they create also serve the availability queries.
    }
}
