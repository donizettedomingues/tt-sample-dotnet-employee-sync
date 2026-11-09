namespace EmployeeSync.Domain.Employees;

/// <summary>Upstream HR providers that can push employees to us.</summary>
public enum Provider
{
    Provider1 = 1,
    Provider2 = 2,
}

public static class ProviderNames
{
    public const string Provider1 = "provider1";
    public const string Provider2 = "provider2";

    public static bool TryParse(string? value, out Provider provider)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case Provider1:
                provider = Provider.Provider1;
                return true;
            case Provider2:
                provider = Provider.Provider2;
                return true;
            default:
                provider = default;
                return false;
        }
    }

    public static string ToRouteValue(this Provider provider) => provider switch
    {
        Provider.Provider1 => Provider1,
        Provider.Provider2 => Provider2,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider"),
    };
}
