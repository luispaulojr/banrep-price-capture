using BanRepPriceCapture.ApplicationLayer.Jobs;
using BanRepPriceCapture.ApplicationLayer.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace BanRepPriceCapture.ApplicationLayer.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddScoped<DtfDailyJob>();
        services.AddScoped<DtfWeeklyJob>();
        services.AddScoped<DtfDailyCaptureWorkflow>();
        services.AddScoped<DtfSeriesWorkflow>();

        return services;
    }
}
