using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(100);
        builder.HasIndex(s => s.Name).IsUnique();
    }
}

internal sealed class CapabilityConfiguration : IEntityTypeConfiguration<Capability>
{
    public void Configure(EntityTypeBuilder<Capability> builder)
    {
        builder.ToTable("capabilities");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(100);
        builder.HasIndex(c => c.Name).IsUnique();
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(320);
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(v => v.CustomerId).HasColumnName("customer_id");
        builder.Property(v => v.Vin).HasColumnName("vin").HasMaxLength(64);
        builder.Property(v => v.Make).HasColumnName("make").HasMaxLength(100);
        builder.Property(v => v.Model).HasColumnName("model").HasMaxLength(100);
        builder.HasIndex(v => v.Vin).IsUnique();
        builder.HasOne<Customer>().WithMany().HasForeignKey(v => v.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DealershipConfiguration : IEntityTypeConfiguration<Dealership>
{
    public void Configure(EntityTypeBuilder<Dealership> builder)
    {
        builder.ToTable("dealerships");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(d => d.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(100);
        builder.HasMany(d => d.WorkingHours).WithOne().HasForeignKey(w => w.DealershipId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WorkingHoursConfiguration : IEntityTypeConfiguration<WorkingHours>
{
    public void Configure(EntityTypeBuilder<WorkingHours> builder)
    {
        builder.ToTable("working_hours", t =>
            t.HasCheckConstraint("ck_working_hours_opens_before_closes", "opens_at < closes_at"));
        builder.HasKey(w => new { w.DealershipId, w.DayOfWeek });
        builder.Property(w => w.DealershipId).HasColumnName("dealership_id");
        builder.Property(w => w.DayOfWeek).HasColumnName("day_of_week");
        builder.Property(w => w.OpensAt).HasColumnName("opens_at");
        builder.Property(w => w.ClosesAt).HasColumnName("closes_at");
    }
}

internal sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.ToTable("service_types", t => t.HasCheckConstraint("ck_service_types_duration_positive", "duration > interval '0'"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(s => s.Duration).HasColumnName("duration");

        builder.HasMany(s => s.RequiredSkills).WithMany().UsingEntity<Dictionary<string, object>>(
            "service_type_required_skills",
            r => r.HasOne<Skill>().WithMany().HasForeignKey("skill_id"),
            l => l.HasOne<ServiceType>().WithMany().HasForeignKey("service_type_id"),
            j => j.HasKey("service_type_id", "skill_id"));

        builder.HasMany(s => s.RequiredCapabilities).WithMany().UsingEntity<Dictionary<string, object>>(
            "service_type_required_capabilities",
            r => r.HasOne<Capability>().WithMany().HasForeignKey("capability_id"),
            l => l.HasOne<ServiceType>().WithMany().HasForeignKey("service_type_id"),
            j => j.HasKey("service_type_id", "capability_id"));
    }
}

internal sealed class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.ToTable("technicians");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.DealershipId).HasColumnName("dealership_id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200);
        builder.HasIndex(t => t.DealershipId);
        builder.HasOne<Dealership>().WithMany().HasForeignKey(t => t.DealershipId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Skills).WithMany().UsingEntity<Dictionary<string, object>>(
            "technician_skills",
            r => r.HasOne<Skill>().WithMany().HasForeignKey("skill_id"),
            l => l.HasOne<Technician>().WithMany().HasForeignKey("technician_id"),
            j => j.HasKey("technician_id", "skill_id"));
    }
}

internal sealed class ServiceBayConfiguration : IEntityTypeConfiguration<ServiceBay>
{
    public void Configure(EntityTypeBuilder<ServiceBay> builder)
    {
        builder.ToTable("service_bays");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.DealershipId).HasColumnName("dealership_id");
        builder.Property(b => b.Name).HasColumnName("name").HasMaxLength(200);
        builder.HasIndex(b => b.DealershipId);
        builder.HasOne<Dealership>().WithMany().HasForeignKey(b => b.DealershipId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Capabilities).WithMany().UsingEntity<Dictionary<string, object>>(
            "service_bay_capabilities",
            r => r.HasOne<Capability>().WithMany().HasForeignKey("capability_id"),
            l => l.HasOne<ServiceBay>().WithMany().HasForeignKey("service_bay_id"),
            j => j.HasKey("service_bay_id", "capability_id"));
    }
}
