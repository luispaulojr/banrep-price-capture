using BanRepPriceCapture.DtfWeeklyPoc.Shared.Flow;

namespace BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Http;

public sealed class FlowIdDelegatingHandler(IFlowContextAccessor flowContext) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (flowContext.FlowId != Guid.Empty)
        {
            request.Headers.TryAddWithoutValidation(FlowConstants.FlowIdHeaderName, flowContext.FlowId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
