using System.Threading;

namespace BanRepPriceCapture.ApplicationLayer.Flow;

public sealed class FlowContextAccessor : IFlowContextAccessor
{
    private static readonly AsyncLocal<Guid> CurrentFlowId = new();
    private static readonly AsyncLocal<DateOnly?> CurrentCaptureDate = new();

    public Guid FlowId => CurrentFlowId.Value == Guid.Empty ? Guid.Empty : CurrentFlowId.Value;
    public DateOnly? CaptureDate => CurrentCaptureDate.Value;

    public void SetFlowId(Guid flowId)
    {
        CurrentFlowId.Value = flowId;
    }

    public void SetCaptureDate(DateOnly captureDate)
    {
        CurrentCaptureDate.Value = captureDate;
    }
}
