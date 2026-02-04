using System.Threading;

namespace BanRepPriceCapture.DtfWeeklyPoc.Shared.Flow;

public sealed class FlowContextAccessor : IFlowContextAccessor
{
    private static readonly AsyncLocal<Guid> CurrentFlowId = new();

    public Guid FlowId => CurrentFlowId.Value == Guid.Empty ? Guid.Empty : CurrentFlowId.Value;

    public void SetFlowId(Guid flowId)
    {
        CurrentFlowId.Value = flowId;
    }
}
