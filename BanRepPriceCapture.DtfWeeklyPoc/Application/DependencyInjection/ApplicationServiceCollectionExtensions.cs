using BanRepPriceCapture.DtfWeeklyPoc.Application.Jobs;
using BanRepPriceCapture.DtfWeeklyPoc.Application.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace BanRepPriceCapture.DtfWeeklyPoc.Application.DependencyInjection;

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
