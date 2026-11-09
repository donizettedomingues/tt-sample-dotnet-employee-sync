using EmployeeSync.Application.Abstractions.Messaging;
using EmployeeSync.Application.Employees;
using EmployeeSync.Application.Employees.Commands;
using EmployeeSync.Application.Employees.Mapping;
using EmployeeSync.Application.Employees.Queries;
using EmployeeSync.Application.Employees.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmployeeSync.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IProviderEmployeeMapper, Provider1EmployeeMapper>();
        services.AddSingleton<IProviderEmployeeMapper, Provider2EmployeeMapper>();
        services.AddSingleton<ProviderMapperResolver>();

        services.AddScoped<EmployeeSyncService>();
        services.AddScoped<ICommandHandler<SubmitEmployeeCommand, SubmitEmployeeResult>, SubmitEmployeeHandler>();
        services.AddScoped<IQueryHandler<GetEmployeeQuery, EmployeeDto?>, GetEmployeeHandler>();

        return services;
    }
}
