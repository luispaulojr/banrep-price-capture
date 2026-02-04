using BanRepPriceCapture.ApplicationLayer.DependencyInjection;
using BanRepPriceCapture.DomainLayer.DependencyInjection;
using BanRepPriceCapture.InfrastructureLayer.DependencyInjection;
using BanRepPriceCapture.InfrastructureLayer.Logging;
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
        services.AddTransient<FlowIdMiddleware>();

        return services;
    }
}
