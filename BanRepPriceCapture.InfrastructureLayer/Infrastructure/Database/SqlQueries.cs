using System.Collections.ObjectModel;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Database;

internal static class SqlQueries
{
    private static readonly IReadOnlyDictionary<string, string> Queries = LoadQueries();

    public static string InsertDtfDailyPrice => GetQuery(nameof(InsertDtfDailyPrice));
    public static string GetDtfDailyPricePayloadsByFlowId => GetQuery(nameof(GetDtfDailyPricePayloadsByFlowId));
    public static string InsertProcessingState => GetQuery(nameof(InsertProcessingState));
    public static string UpdateProcessingStateStatus => GetQuery(nameof(UpdateProcessingStateStatus));
    public static string RecordProcessingStateSend => GetQuery(nameof(RecordProcessingStateSend));
    public static string GetProcessingStateByFlowId => GetQuery(nameof(GetProcessingStateByFlowId));
    public static string GetLastProcessingStateByCaptureDate => GetQuery(nameof(GetLastProcessingStateByCaptureDate));
    public static string ListFailedOrIncompleteExecutions => GetQuery(nameof(ListFailedOrIncompleteExecutions));

    private static string GetQuery(string name)
    {
        if (!Queries.TryGetValue(name, out var query))
        {
            throw new InvalidOperationException($"SQL query '{name}' not found.");
        }

        return query;
    }

    private static IReadOnlyDictionary<string, string> LoadQueries()
    {
        var assembly = typeof(SqlQueries).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .Single(resource => resource.EndsWith("queries.sql", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("queries.sql resource not found.");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        return new ReadOnlyDictionary<string, string>(ParseQueries(content));
    }

    private static Dictionary<string, string> ParseQueries(string content)
    {
        var queries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sections = content.Split("-- name:", StringSplitOptions.RemoveEmptyEntries);

        foreach (var section in sections)
        {
            var trimmedSection = section.Trim();
            if (string.IsNullOrEmpty(trimmedSection))
            {
                continue;
            }

            var newlineIndex = trimmedSection.IndexOf('\n');
            if (newlineIndex < 0)
            {
                continue;
            }

            var name = trimmedSection[..newlineIndex].Trim();
            var sql = trimmedSection[(newlineIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sql))
            {
                continue;
            }

            queries[name] = sql;
        }

        return queries;
    }
}
