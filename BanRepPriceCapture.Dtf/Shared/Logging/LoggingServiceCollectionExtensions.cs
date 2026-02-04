using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BanRepPriceCapture.Dtf.Shared.Logging;

public static class LoggingServiceCollectionExtensions
{
    public static IServiceCollection AddStructuredLogging(this IServiceCollection services)
    {
        services.AddSingleton<IStructuredLogger, StructuredLogger>();
        return services;
    }

    public static IServiceCollection AddLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddStructuredLogging();
        services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        return services;
    }
}
