namespace BanRepPriceCapture.ApplicationLayer.Flow;

public interface IFlowContextAccessor
{
    Guid FlowId { get; }
    DateOnly? CaptureDate { get; }
    void SetFlowId(Guid flowId);
    void SetCaptureDate(DateOnly captureDate);
}
