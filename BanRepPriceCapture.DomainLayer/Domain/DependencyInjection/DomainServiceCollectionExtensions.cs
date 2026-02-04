using Microsoft.Extensions.DependencyInjection;

namespace BanRepPriceCapture.DomainLayer.Domain.DependencyInjection;

public static class DomainServiceCollectionExtensions
{
    public static IServiceCollection AddDomainLayer(this IServiceCollection services)
    {
        return services;
    }
}
