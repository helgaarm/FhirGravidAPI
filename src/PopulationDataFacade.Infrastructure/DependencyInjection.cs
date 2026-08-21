using Duende.AccessTokenManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure.Configuration;
using PopulationDataFacade.Infrastructure.Dhg;
using PopulationDataFacade.Infrastructure.HelseId;

namespace PopulationDataFacade.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPopulationDataFacadeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DhgOptions>()
            .Bind(configuration.GetSection(DhgOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<HelseIdOptions>()
            .Bind(configuration.GetSection(HelseIdOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<DevelopmentTestModeOptions>()
            .Bind(configuration.GetSection(DevelopmentTestModeOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Subject),
                "DevelopmentTestMode:Subject is required when test mode is enabled.")
            .ValidateOnStart();
        services.AddOptions<HelseIdTestTokenOptions>()
            .Bind(configuration.GetSection(HelseIdTestTokenOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DhgOptions>, DhgOptionsValidator>();
        services.AddSingleton<IValidateOptions<HelseIdOptions>>(
            new HelseIdOptionsValidator(() =>
                configuration.GetValue<bool>($"{DevelopmentTestModeOptions.SectionName}:Enabled") &&
                configuration.GetValue<bool>($"{HelseIdTestTokenOptions.SectionName}:Enabled")));
        services.AddSingleton<IValidateOptions<HelseIdTestTokenOptions>>(
            new HelseIdTestTokenOptionsValidator(
                () => configuration.GetValue<bool>($"{DevelopmentTestModeOptions.SectionName}:Enabled"),
                () => configuration[$"{DhgOptions.SectionName}:Environment"] ?? "Test"));

        // Token exchange stays request-scoped and is deliberately not put in a facade-level token cache.
        services.AddClientCredentialsTokenManagement();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IHelseIdClientAssertionFactory, HelseIdClientAssertionFactory>();
        services.AddScoped<HelseIdAuthorizationProvider>();
        services.AddScoped<HelseIdTestTokenAuthorizationProvider>();
        services.AddScoped<IDhgAuthorizationProvider>(provider =>
        {
            var developmentTestMode = provider.GetRequiredService<IOptions<DevelopmentTestModeOptions>>().Value;
            var testTokenUtility = provider.GetRequiredService<IOptions<HelseIdTestTokenOptions>>().Value;
            return developmentTestMode.Enabled && testTokenUtility.Enabled
                ? provider.GetRequiredService<HelseIdTestTokenAuthorizationProvider>()
                : provider.GetRequiredService<HelseIdAuthorizationProvider>();
        });

        services.AddHttpClient("HelseIdBackchannel", client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(5),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AutomaticDecompression = System.Net.DecompressionMethods.All
            });

        services.AddHttpClient("HelseIdTestToken", client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(5),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AutomaticDecompression = System.Net.DecompressionMethods.All
            });

        services.AddHttpClient<IDhgClient, DhgHttpClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<DhgOptions>>().Value;
                client.BaseAddress = options.BaseUrl;
                client.Timeout = options.RequestTimeout;
            })
            .ConfigurePrimaryHttpMessageHandler(provider =>
            {
                var options = provider.GetRequiredService<IOptions<DhgOptions>>().Value;
                return new SocketsHttpHandler
                {
                    ConnectTimeout = options.ConnectTimeout,
                    PooledConnectionLifetime = options.PooledConnectionLifetime,
                    AutomaticDecompression = System.Net.DecompressionMethods.All
                };
            });

        services.AddSingleton<DhgPopulationSnapshotFactory>();
        services.AddScoped<IPopulationDataService, DhgPopulationDataService>();
        return services;
    }
}
