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

        services.Configure<BearerTokenSettings>(configuration.GetSection("BearerToken"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<BearerTokenSettings>>().Value);

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

        services.AddHttpClient<ISdmxClient, BanRepSdmxClient>(http =>
            {
                http.BaseAddress = new Uri("https://totoro.banrep.gov.co/nsi-jax-ws/rest/data/");
                http.Timeout = TimeSpan.FromSeconds(30);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("BTG-DTF-Weekly-POC/.NET8");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
            })
            .AddHttpMessageHandler<HttpLoggingHandler>()
            .AddHttpMessageHandler<FlowIdDelegatingHandler>();

        services.AddHttpClient<IDtfDailyOutboundClient, DtfDailyOutboundClient>()
            .AddHttpMessageHandler<HttpLoggingHandler>()
            .AddHttpMessageHandler<FlowIdDelegatingHandler>()
            .AddHttpMessageHandler<BearerTokenHandler>();

        services.AddTransient<IDtfDailyPayloadSender, DtfDailyPayloadSender>();

        services.AddTransient<HttpLoggingHandler>();
        services.AddTransient<FlowIdDelegatingHandler>();
        services.AddTransient<BearerTokenHandler>();

        services.AddScoped<IDtfDailyPriceRepository, DtfDailyPriceRepository>();

        services.AddSingleton<INotificationService, NotificationServiceStub>();

        services.AddHostedService<DtfDailyRabbitConsumer>();

        return services;
    }
}
