namespace BanRepPriceCapture.ApplicationLayer.Flow;

public interface IFlowIdProvider
{
    Guid CreateFromHeader(string? headerValue);
    Guid CreateFromMessageId(string? messageId);
}
