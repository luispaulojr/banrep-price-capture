using Amazon.SecretsManager;
using BanRepPriceCapture.ApplicationLayer.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Notifications;
using BanRepPriceCapture.InfrastructureLayer.Aws;
using BanRepPriceCapture.InfrastructureLayer.Clients;
using BanRepPriceCapture.InfrastructureLayer.Database;
using BanRepPriceCapture.InfrastructureLayer.Database.TypeHandlers;
using BanRepPriceCapture.InfrastructureLayer.Http;
using BanRepPriceCapture.InfrastructureLayer.Messaging;
using BanRepPriceCapture.InfrastructureLayer.Notifications;
using BanRepPriceCapture.InfrastructureLayer.Outbound;
using BanRepPriceCapture.InfrastructureLayer.Repositories;
using BanRepPriceCapture.InfrastructureLayer.Configuration;
using BanRepPriceCapture.InfrastructureLayer.Resilience;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BanRepPriceCapture.InfrastructureLayer.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DtfDailyCaptureSettings>(configuration.GetSection("DtfDailyCapture"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DtfDailyCaptureSettings>>().Value);

        services.Configure<RabbitMqSettings>(configuration.GetSection("RabbitMq"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value);

        services.Configure<DatabaseSettings>(configuration.GetSection("Database"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DatabaseSettings>>().Value);

        services.Configure<DatabaseSecretSettings>(configuration.GetSection("DatabaseSecrets"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DatabaseSecretSettings>>().Value);

        services.AddOptions<ExternalServicesSettings>()
            .Bind(configuration.GetSection(ExternalServicesSettings.SectionName))
            .ValidateOnStart();

        services.AddOptions<NotificationServiceSettings>()
            .Bind(configuration.GetSection(ExternalServicesSettings.NotificationSectionPath))
            .Validate(settings => string.IsNullOrWhiteSpace(settings.BaseUrl)
                || Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
                "ExternalServices:Notification:BaseUrl must be a valid absolute URL.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<NotificationServiceSettings>>().Value);

        services.AddOptions<SdmxServiceSettings>()
            .Bind(configuration.GetSection(ExternalServicesSettings.SdmxSectionPath))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.BaseUrl)
                && Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
                "ExternalServices:Sdmx:BaseUrl must be configured with a valid absolute URL.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<SdmxServiceSettings>>().Value);

        services.AddOptions<DtfDailyOutboundServiceSettings>()
            .Bind(configuration.GetSection(ExternalServicesSettings.DtfDailyOutboundSectionPath))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.BaseUrl)
                && Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
                "ExternalServices:DtfDailyOutbound:BaseUrl must be configured with a valid absolute URL.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DtfDailyOutboundServiceSettings>>().Value);

        services.AddSingleton<IFlowContextAccessor, FlowContextAccessor>();
        services.AddSingleton<IFlowIdProvider, FlowIdProvider>();

        services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
        services.AddSingleton<IDatabaseSecretsProvider, AwsSecretsManagerDatabaseSecretsProvider>();
        services.AddSingleton<IDatabaseConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IRetryPolicyProvider, RetryPolicyProvider>();

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());

        services.AddSingleton<IConnectionFactory>(sp =>
        {
            var settings = sp.GetRequiredService<RabbitMqSettings>();
            var userName = string.IsNullOrWhiteSpace(settings.UserName)
                ? Environment.GetEnvironmentVariable(settings.UserNameEnvVar)
                : settings.UserName;
            var password = string.IsNullOrWhiteSpace(settings.Password)
                ? Environment.GetEnvironmentVariable(settings.PasswordEnvVar)
                : settings.Password;

            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new InvalidOperationException($"RabbitMQ username not configured. Set {settings.UserNameEnvVar} or RabbitMq:UserName.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException($"RabbitMQ password not configured. Set {settings.PasswordEnvVar} or RabbitMq:Password.");
            }

            return new ConnectionFactory
            {
                HostName = settings.HostName,
                Port = settings.Port,
                UserName = userName,
                Password = password,
                VirtualHost = settings.VirtualHost,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true
            };
        });

        services.AddHttpClient<ISdmxClient, BanRepSdmxClient>((sp, http) =>
            {
                var settings = sp.GetRequiredService<SdmxServiceSettings>();
                http.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
                http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds ?? 30);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("BTG-DTF-Weekly-POC/.NET8");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
            })
            .AddHttpMessageHandler<HttpLoggingHandler>()
            .AddHttpMessageHandler<FlowIdDelegatingHandler>();

        services.AddHttpClient<IDtfDailyOutboundClient, DtfDailyOutboundClient>((sp, http) =>
            {
                var settings = sp.GetRequiredService<DtfDailyOutboundServiceSettings>();
                if (settings.TimeoutSeconds.HasValue)
                {
                    http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds.Value);
                }
            })
            .AddHttpMessageHandler<HttpLoggingHandler>()
            .AddHttpMessageHandler<FlowIdDelegatingHandler>()
            .AddHttpMessageHandler<BearerTokenHandler>();

        services.AddTransient<IDtfDailyPayloadSender, DtfDailyPayloadSender>();

        services.AddHttpClient<HttpNotificationService>((sp, http) =>
            {
                var settings = sp.GetRequiredService<NotificationServiceSettings>();
                if (Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
                {
                    http.BaseAddress = baseUri;
                }

                if (settings.TimeoutSeconds.HasValue)
                {
                    http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds.Value);
                }
            })
            .AddHttpMessageHandler<HttpLoggingHandler>()
            .AddHttpMessageHandler<FlowIdDelegatingHandler>();

        services.AddTransient<HttpLoggingHandler>();
        services.AddTransient<FlowIdDelegatingHandler>();
        services.AddTransient<BearerTokenHandler>();

        services.AddScoped<IDtfDailyPriceRepository, DtfDailyPriceRepository>();

        services.AddSingleton<INotificationService>(sp =>
        {
            var settings = sp.GetRequiredService<NotificationServiceSettings>();
            var logger = sp.GetRequiredService<IStructuredLogger>();

            if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
            {
                logger.LogWarning(
                    method: "NotificationService.Configuration",
                    description: "NotificationService BaseUrl nao configurado.",
                    message: "Using NotificationServiceStub.");
                return new NotificationServiceStub(logger);
            }

            return sp.GetRequiredService<HttpNotificationService>();
        });

        services.AddHostedService<DtfDailyRabbitConsumer>();

        return services;
    }
}
