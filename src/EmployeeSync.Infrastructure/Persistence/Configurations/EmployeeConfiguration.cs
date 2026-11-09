using EmployeeSync.Application.Employees.Mapping;
using EmployeeSync.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployeeSync.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Enums are stored as their API/TrackTik strings so the table (and the T-SQL report) is readable.
        builder.Property(e => e.Provider)
            .HasConversion(p => p.ToRouteValue(), s => ParseProvider(s))
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(e => e.ProviderEmployeeId).HasMaxLength(FieldLimits.ProviderEmployeeId).IsRequired();
        builder.Property(e => e.TrackTikEmployeeId).HasMaxLength(FieldLimits.ProviderEmployeeId + 21).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(FieldLimits.Name).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(FieldLimits.Name).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(FieldLimits.Email).IsRequired();
        builder.Property(e => e.PhoneNumber).HasMaxLength(FieldLimits.Phone);
        builder.Property(e => e.Position).HasMaxLength(FieldLimits.Position);
        builder.Property(e => e.Department).HasMaxLength(FieldLimits.Department);

        builder.Property(e => e.Status)
            .HasConversion(s => s.ToTrackTikValue(), s => ParseEmploymentStatus(s))
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(e => e.SyncStatus)
            .HasConversion(s => s.ToApiValue(), s => ParseSyncStatus(s))
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(e => e.LastError).HasMaxLength(Employee.MaxErrorLength);
        builder.Property(e => e.TrackTikRecordId).HasMaxLength(64);

        // Identity of an employee: one row per (provider, providerEmployeeId). Never merged across providers.
        builder.HasIndex(e => new { e.Provider, e.ProviderEmployeeId })
            .IsUnique()
            .HasDatabaseName("UX_Employees_Provider_ProviderEmployeeId");

        builder.HasIndex(e => e.SyncStatus).HasDatabaseName("IX_Employees_SyncStatus");

        builder.Ignore(e => e.DomainEvents);
    }

    private static Provider ParseProvider(string value) =>
        ProviderNames.TryParse(value, out var provider)
            ? provider
            : throw new InvalidOperationException($"Unknown provider '{value}' in database.");

    private static EmploymentStatus ParseEmploymentStatus(string value) => value switch
    {
        "active" => EmploymentStatus.Active,
        "inactive" => EmploymentStatus.Inactive,
        "terminated" => EmploymentStatus.Terminated,
        _ => throw new InvalidOperationException($"Unknown employment status '{value}' in database."),
    };

    private static SyncStatus ParseSyncStatus(string value) => value switch
    {
        "pending" => SyncStatus.Pending,
        "synced" => SyncStatus.Synced,
        "failed" => SyncStatus.Failed,
        _ => throw new InvalidOperationException($"Unknown sync status '{value}' in database."),
    };
}
