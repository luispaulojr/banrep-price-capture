namespace BanRepPriceCapture.InfrastructureLayer.Configuration;

public sealed record RabbitMqSettings
{
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string VirtualHost { get; init; } = "/";
    public string UserNameEnvVar { get; init; } = "BANREP_RABBITMQ_USERNAME";
    public string PasswordEnvVar { get; init; } = "BANREP_RABBITMQ_PASSWORD";
}
