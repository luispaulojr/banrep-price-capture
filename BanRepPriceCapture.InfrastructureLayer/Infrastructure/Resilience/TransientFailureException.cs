namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Resilience;

public sealed class TransientFailureException : Exception
{
    public TransientFailureException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
