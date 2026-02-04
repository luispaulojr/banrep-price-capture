using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using BanRepPriceCapture.InfrastructureLayer.Database;
using BanRepPriceCapture.InfrastructureLayer.Configuration;

namespace BanRepPriceCapture.InfrastructureLayer.Aws;

public sealed class AwsSecretsManagerDatabaseSecretsProvider(
    IAmazonSecretsManager secretsManager,
    DatabaseSecretSettings settings) : IDatabaseSecretsProvider
{
    public async Task<DatabaseSecrets> GetSecretsAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.SecretId))
        {
            return ReadFromEnvironment(settings);
        }

        var request = new GetSecretValueRequest
        {
            SecretId = settings.SecretId
        };

        var response = await secretsManager.GetSecretValueAsync(request, ct);
        var secretJson = response.SecretString ?? string.Empty;

        if (string.IsNullOrWhiteSpace(secretJson))
        {
            return ReadFromEnvironment(settings);
        }

        var document = JsonDocument.Parse(secretJson);
        var root = document.RootElement;

        var host = GetValue(root, "host");
        var hostRo = GetValue(root, "host_ro");
        var username = GetValue(root, "username");
        var password = GetValue(root, "password");

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(hostRo) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return ReadFromEnvironment(settings);
        }

        return new DatabaseSecrets(host, hostRo, username, password);
    }

    private static DatabaseSecrets ReadFromEnvironment(DatabaseSecretSettings settings)
    {
        var host = Environment.GetEnvironmentVariable(settings.HostEnvVar) ?? string.Empty;
        var hostRo = Environment.GetEnvironmentVariable(settings.HostReadOnlyEnvVar) ?? string.Empty;
        var username = Environment.GetEnvironmentVariable(settings.UsernameEnvVar) ?? string.Empty;
        var password = Environment.GetEnvironmentVariable(settings.PasswordEnvVar) ?? string.Empty;

        return new DatabaseSecrets(host, hostRo, username, password);
    }

    private static string GetValue(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    }
}
