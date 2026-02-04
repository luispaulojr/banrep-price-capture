using BanRepPriceCapture.DtfWeeklyPoc.Shared.Flow;

namespace BanRepPriceCapture.DtfWeeklyPoc.Presentation.Middleware;

public sealed class FlowIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IFlowContextAccessor flowContext)
    {
        var header = context.Request.Headers[FlowConstants.FlowIdHeaderName].FirstOrDefault();
        var flowId = TryParse(header) ?? Guid.NewGuid();

        flowContext.SetFlowId(flowId);

        await next(context);
    }

    private static Guid? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }
}
