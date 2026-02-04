namespace BanRepPriceCapture.ApplicationLayer.Flow;

public sealed class FlowIdProvider : IFlowIdProvider
{
    public Guid CreateFromHeader(string? headerValue) => TryParse(headerValue) ?? Guid.NewGuid();

    public Guid CreateFromMessageId(string? messageId) => TryParse(messageId) ?? Guid.NewGuid();

    private static Guid? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }
}
