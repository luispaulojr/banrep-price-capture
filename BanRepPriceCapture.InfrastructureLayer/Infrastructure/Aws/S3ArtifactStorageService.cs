using Amazon.S3;
using Amazon.S3.Model;
using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Logging;
using BanRepPriceCapture.InfrastructureLayer.Configuration;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Aws;

public sealed class S3ArtifactStorageService(
    IAmazonS3 s3Client,
    S3ArtifactStorageSettings settings,
    IStructuredLogger logger) : IArtifactStorageService
{
    private const int DefaultMaxAttempts = 3;
    private const int DefaultBackoffSeconds = 2;

    public async Task UploadDtfDailyCsvAsync(Guid flowId, DateOnly captureDate, string csvPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.BucketName)
            || string.IsNullOrWhiteSpace(settings.Environment))
        {
            logger.LogWarning(
                method: "S3ArtifactStorageService.UploadDtfDailyCsvAsync",
                description: "Configuracao do S3 nao informada.",
                message: $"flowId={flowId} arquivo={csvPath}");
            return;
        }

        var key = $"{settings.Environment}/dtf/{captureDate:yyyyMMdd}/{flowId:N}.csv";
        var request = new PutObjectRequest
        {
            BucketName = settings.BucketName,
            Key = key,
            FilePath = csvPath,
            ContentType = "text/csv"
        };

        await ExecuteWithRetryAsync(
            flowId,
            () => s3Client.PutObjectAsync(request, ct),
            key,
            ct);
    }

    private async Task ExecuteWithRetryAsync(
        Guid flowId,
        Func<Task<PutObjectResponse>> operation,
        string key,
        CancellationToken ct)
    {
        var maxAttempts = settings.Retry.MaxAttempts ?? DefaultMaxAttempts;
        var backoffSeconds = settings.Retry.BackoffSeconds ?? DefaultBackoffSeconds;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                logger.LogInformation(
                    method: "S3ArtifactStorageService.UploadDtfDailyCsvAsync",
                    description: "Enviando CSV para S3.",
                    message: $"flowId={flowId} bucket={settings.BucketName} chave={key} tentativa={attempt}/{maxAttempts}");

                await operation();

                logger.LogInformation(
                    method: "S3ArtifactStorageService.UploadDtfDailyCsvAsync",
                    description: "Upload do CSV concluido.",
                    message: $"flowId={flowId} bucket={settings.BucketName} chave={key}");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(backoffSeconds * Math.Pow(2, attempt - 1));
                logger.LogWarning(
                    method: "S3ArtifactStorageService.UploadDtfDailyCsvAsync",
                    description: "Falha no upload para S3, tentando novamente.",
                    message: $"flowId={flowId} bucket={settings.BucketName} chave={key} espera={delay.TotalSeconds}s",
                    exception: ex);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    method: "S3ArtifactStorageService.UploadDtfDailyCsvAsync",
                    description: "Falha no upload para S3.",
                    message: $"flowId={flowId} bucket={settings.BucketName} chave={key}",
                    exception: ex);
                throw;
            }
        }
    }
}
