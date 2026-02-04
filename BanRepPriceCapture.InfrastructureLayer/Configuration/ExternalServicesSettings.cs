namespace BanRepPriceCapture.InfrastructureLayer.Configuration;

public sealed record ExternalServicesSettings
{
    public const string SectionName = "ExternalServices";
    public const string NotificationSectionName = "Notification";
    public const string SdmxSectionName = "Sdmx";
    public const string DtfDailyOutboundSectionName = "DtfDailyOutbound";

    public static string NotificationSectionPath => $"{SectionName}:{NotificationSectionName}";
    public static string SdmxSectionPath => $"{SectionName}:{SdmxSectionName}";
    public static string DtfDailyOutboundSectionPath => $"{SectionName}:{DtfDailyOutboundSectionName}";

    public NotificationServiceSettings Notification { get; init; } = new();
    public SdmxServiceSettings Sdmx { get; init; } = new();
    public DtfDailyOutboundServiceSettings DtfDailyOutbound { get; init; } = new();
}

public abstract record ExternalServiceSettings
{
    public string BaseUrl { get; init; } = string.Empty;
    public string? SystemId { get; init; }
    public ExternalServiceSecretsSettings Secrets { get; init; } = new();
    public ExternalServiceAuthenticationSettings Authentication { get; init; } = new();
    public int? TimeoutSeconds { get; init; }
    public ExternalServiceRetrySettings Retry { get; init; } = new();
}

public sealed record NotificationServiceSettings : ExternalServiceSettings;

public sealed record SdmxServiceSettings : ExternalServiceSettings;

public sealed record DtfDailyOutboundServiceSettings : ExternalServiceSettings;

public sealed record ExternalServiceSecretsSettings
{
    public string? SecretName { get; init; }
    public string? ApiKey { get; init; }
    public string? ApiKeyEnvVar { get; init; }
    public string? ApiKeySecretName { get; init; }
}

public sealed record ExternalServiceAuthenticationSettings
{
    public string? Scheme { get; init; }
    public string? ApiKey { get; init; }
    public string? ApiKeyEnvVar { get; init; }
    public string? ApiKeySecretName { get; init; }
    public string? TokenEnvVar { get; init; }
    public string? TokenSecretName { get; init; }
}

public sealed record ExternalServiceRetrySettings
{
    public int? MaxAttempts { get; init; }
    public int? BackoffSeconds { get; init; }
}
