using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceScheduler.Application.Interfaces;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    /// <summary>The unique constraint on the key; a violation of it means "same Idempotency-Key committed concurrently".</summary>
    public const string KeyConstraintName = "pk_idempotency_records";

    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(r => r.Key).HasName(KeyConstraintName);
        builder.Property(r => r.Key).HasColumnName("key").HasMaxLength(255).ValueGeneratedNever();
        builder.Property(r => r.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(r => r.ResponseJson).HasColumnName("response_json").HasColumnType("text");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
    }
}
