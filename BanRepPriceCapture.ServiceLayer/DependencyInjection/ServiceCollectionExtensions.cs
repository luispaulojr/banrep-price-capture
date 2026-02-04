using BanRepPriceCapture.InfrastructureLayer.Logging;
using BanRepPriceCapture.ServiceLayer.Presentation.HealthChecks;
using BanRepPriceCapture.ServiceLayer.Presentation.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BanRepPriceCapture.ServiceLayer.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServiceLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddLogging(configuration);
        services.AddApplicationLayer();
        services.AddDomainLayer();
        services.AddInfrastructureLayer(configuration);
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<RabbitMqHealthCheck>("rabbitmq")
            .AddCheck<SdmxHealthCheck>("sdmx");
        services.AddTransient<FlowIdMiddleware>();

        return services;
    }
}
