namespace BanRepPriceCapture.Dtf.Shared.Flow;

public interface IFlowContextAccessor
{
    Guid FlowId { get; }
    void SetFlowId(Guid flowId);
}
