using EmployeeSync.Application.Abstractions.Events;
using EmployeeSync.Application.Abstractions.Persistence;
using EmployeeSync.Application.Abstractions.TrackTik;
using EmployeeSync.Infrastructure.Events;
using EmployeeSync.Infrastructure.Persistence;
using EmployeeSync.Infrastructure.TrackTik;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EmployeeSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        AddPersistence(services, configuration);
        AddTrackTik(services, configuration);
        AddEvents(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EmployeeSync")
            ?? throw new InvalidOperationException("Connection string 'EmployeeSync' is not configured (ConnectionStrings__EmployeeSync).");

        services.AddDbContext<EmployeeSyncDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5)));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<EmployeeSyncDbContext>());
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeReadStore, EmployeeReadStore>();
    }

    private static void AddTrackTik(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TrackTikOptions>()
            .Bind(configuration.GetSection(TrackTikOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(TrackTikAccessTokenProvider.HttpClientName, (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<TrackTikOptions>>().Value.TimeoutSeconds);
        });
        services.AddSingleton<ITrackTikAccessTokenProvider, TrackTikAccessTokenProvider>();
        services.AddTransient<TrackTikAuthenticationHandler>();

        services.AddHttpClient<ITrackTikClient, TrackTikClient>(TrackTikClient.HttpClientName, (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<TrackTikOptions>>().Value;
                // Trailing slash so relative paths ("employees") resolve under /v1.
                client.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddHttpMessageHandler<TrackTikAuthenticationHandler>();
    }

    private static void AddEvents(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(EventsOptions.SectionName);
        services.Configure<EventsOptions>(section);

        var publisher = section.GetValue<string>(nameof(EventsOptions.Publisher)) ?? "InMemory";
        if (string.Equals(publisher, "Nats", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEventPublisher, NatsEventPublisher>();
        }
        else
        {
            services.AddSingleton<InMemoryEventPublisher>();
            services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InMemoryEventPublisher>());
        }
    }
}
