using Amazon.SecretsManager;
using BanRepPriceCapture.DtfWeeklyPoc.Application.Interfaces;
using BanRepPriceCapture.DtfWeeklyPoc.Application.Notifications;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Aws;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Clients;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Database;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Database.TypeHandlers;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Http;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Messaging;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Notifications;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Outbound;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Repositories;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Configuration;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Flow;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
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

        services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
        services.AddSingleton<IDatabaseSecretsProvider, AwsSecretsManagerDatabaseSecretsProvider>();
        services.AddSingleton<IDatabaseConnectionFactory, NpgsqlConnectionFactory>();

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());

        services.AddSingleton<IConnectionFactory>(sp =>
        {
            var settings = sp.GetRequiredService<RabbitMqSettings>();
            return new ConnectionFactory
            {
                HostName = settings.HostName,
                Port = settings.Port,
                UserName = settings.UserName,
                Password = settings.Password,
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

        services.AddHttpClient<IDtfDailyPayloadSender, DtfDailyPayloadSender>()
            .AddHttpMessageHandler<HttpLoggingHandler>()
            .AddHttpMessageHandler<FlowIdDelegatingHandler>()
            .AddHttpMessageHandler<BearerTokenHandler>();

        services.AddTransient<HttpLoggingHandler>();
        services.AddTransient<FlowIdDelegatingHandler>();
        services.AddTransient<BearerTokenHandler>();

        services.AddScoped<IDtfDailyPriceRepository, DtfDailyPriceRepository>();

        services.AddSingleton<INotificationService, NotificationServiceStub>();

        services.AddHostedService<DtfDailyRabbitConsumer>();

        return services;
    }
}
