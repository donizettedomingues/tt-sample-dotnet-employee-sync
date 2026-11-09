using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Employees.Mapping;

/// <summary>Picks the mapper strategy for a provider. Adding a provider = one new mapper + DI registration.</summary>
public sealed class ProviderMapperResolver
{
    private readonly IReadOnlyDictionary<Provider, IProviderEmployeeMapper> _mappers;

    public ProviderMapperResolver(IEnumerable<IProviderEmployeeMapper> mappers)
    {
        _mappers = mappers.ToDictionary(m => m.Provider);
    }

    public IProviderEmployeeMapper Resolve(Provider provider) =>
        _mappers.TryGetValue(provider, out var mapper)
            ? mapper
            : throw new InvalidOperationException($"No mapper registered for provider '{provider}'.");
}
