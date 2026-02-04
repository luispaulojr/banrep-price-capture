using BanRepPriceCapture.ApplicationLayer.Application.Jobs;
using BanRepPriceCapture.ApplicationLayer.Application.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace BanRepPriceCapture.ApplicationLayer.Application.DependencyInjection;

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
