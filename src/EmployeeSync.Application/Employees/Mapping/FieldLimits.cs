namespace EmployeeSync.Application.Employees.Mapping;

/// <summary>Column sizes shared by validation and the EF Core model, so they cannot drift apart.</summary>
public static class FieldLimits
{
    public const int ProviderEmployeeId = 64;
    public const int Name = 100;
    public const int Email = 254;
    public const int Phone = 32;
    public const int Position = 100;
    public const int Department = 100;
}
