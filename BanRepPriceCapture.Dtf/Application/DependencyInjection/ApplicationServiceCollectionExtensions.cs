using BanRepPriceCapture.Dtf.Application.Jobs;
using BanRepPriceCapture.Dtf.Application.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace BanRepPriceCapture.Dtf.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<DtfDailyJob>();
        services.AddScoped<DtfWeeklyJob>();
        services.AddScoped<DtfDailyCaptureWorkflow>();

        return services;
    }
}
