namespace BanRepPriceCapture.ApplicationLayer.Flow;

public interface IFlowContextAccessor
{
    Guid FlowId { get; }
    void SetFlowId(Guid flowId);
}
