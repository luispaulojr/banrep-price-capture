using BanRepPriceCapture.ApplicationLayer.Flow;

namespace BanRepPriceCapture.ServiceLayer.Presentation.Middleware;

public sealed class FlowIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IFlowContextAccessor flowContext,
        IFlowIdProvider flowIdProvider)
    {
        var header = context.Request.Headers[FlowConstants.FlowIdHeaderName].FirstOrDefault();
        var flowId = flowIdProvider.CreateFromHeader(header);

        flowContext.SetFlowId(flowId);

        await next(context);
    }
}
