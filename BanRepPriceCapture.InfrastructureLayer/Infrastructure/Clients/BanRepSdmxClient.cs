using System.Globalization;
using System.Net;
using System.Xml.Linq;
using BanRepPriceCapture.ApplicationLayer.Exceptions;
using BanRepPriceCapture.ApplicationLayer.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Models;
using BanRepPriceCapture.InfrastructureLayer.Resilience;

namespace BanRepPriceCapture.InfrastructureLayer.Clients;

public sealed class BanRepSdmxClient(
    HttpClient http,
    IRetryPolicyProvider retryPolicies) : ISdmxClient
{
    // Base REST endpoint do PDF.
    // https://totoro.banrep.gov.co/nsi-jax-ws/rest/data

    private const string AgencyId = "ESTAT";
    private const string Version = "1.0";

    // No PDF, DTF 90 dias historico: DF_DTF_DAILY_HIST
    private const string DtfDailyHistFlowId = "DF_DTF_DAILY_HIST";

    public async Task<List<BanRepSeriesData>> GetDtfDailyAsync(
        DateOnly? start = null,
        DateOnly? end = null,
        CancellationToken ct = default)
    {
        // Monta URL conforme guia:
        // /rest/data/{AGENCY_ID},{FLOW_ID},{VERSION}/all/ALL/?startPeriod=...&endPeriod=...&dimensionAtObservation=TIME_PERIOD&detail=full
        var url = BuildDtfUrl(start, end);

        return await retryPolicies.ExecuteAsync(async token =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.ParseAdd("application/xml");

            HttpResponseMessage resp;
            try
            {
                resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
            }
            catch (TaskCanceledException ex) when (!token.IsCancellationRequested)
            {
                throw new TimeoutException("Timeout ao chamar o SDMX do BanRep.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException("Falha de rede ao chamar o SDMX do BanRep.", ex);
            }

            await using var response = resp;
            var mediaType = response.Content.Headers.ContentType?.MediaType;

            if (mediaType is null || !mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            {
                var body = await SafeReadBody(response, token);
                throw new BanRepSdmxException(
                    $"Resposta inesperada do BanRep. Content-Type={mediaType ?? "<null>"}. Body={(body ?? "<vazio>")}");
            }

            // O PDF descreve erros HTTP comuns: 400, 404, 500, 503
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<BanRepSeriesData>();
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await SafeReadBody(response, token);
                var message =
                    $"Erro do BanRep SDMX. Status={(int)response.StatusCode} {response.ReasonPhrase}. Body={(body ?? "<vazio>")}";

                if (IsTransientStatusCode(response.StatusCode))
                {
                    throw new TransientFailureException(message);
                }

                throw new BanRepSdmxException(message);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token);

            // O serviço retorna SDMX-ML (XML). A observação vem como:
            // <generic:Obs>
            //   <generic:ObsDimension value="2024-08-16" />
            //   <generic:ObsValue value="10.751" />
            // </generic:Obs>
            //
            // A documentação explica que TIME_PERIOD é a dimensão do tempo e OBS_VALUE é o valor.
            return ParseSdmxGenericData(stream);
        }, RetryPolicyKind.SdmxHttp, "BanRepSdmxClient.GetDtfDailyAsync", ct);
    }

    public async Task<List<BanRepSeriesData>> GetDtfWeeklyAsync(
        DateOnly? start = null,
        DateOnly? end = null,
        CancellationToken ct = default)
    {
        var daily = await GetDtfDailyAsync(start, end, ct);

        return AggregateWeeklyByIsoWeek(daily);
    }

    internal static List<BanRepSeriesData> AggregateWeeklyByIsoWeek(IEnumerable<BanRepSeriesData> daily)
    {
        return daily
            .GroupBy(d => IsoWeekKey(d.Date))
            .Select(g => g.OrderBy(x => x.Date).Last())
            .OrderBy(x => x.Date)
            .ToList();
    }

    internal static List<BanRepSeriesData> ParseSdmxGenericData(Stream xmlStream)
    {
        var doc = XDocument.Load(xmlStream);

        // Para ser tolerante com namespaces, usamos LocalName.
        // Busca por todos os elementos Obs e, dentro deles, procura ObsDimension/@value e ObsValue/@value.
        var obs = doc
            .Descendants()
            .Where(e => e.Name.LocalName == "Obs")
            .Select(e =>
            {
                var dim = e.Descendants().FirstOrDefault(x => x.Name.LocalName == "ObsDimension");
                var val = e.Descendants().FirstOrDefault(x => x.Name.LocalName == "ObsValue");

                var dimValue = dim?.Attribute("value")?.Value;
                var obsValue = val?.Attribute("value")?.Value;

                if (string.IsNullOrWhiteSpace(dimValue) || string.IsNullOrWhiteSpace(obsValue))
                    return null;

                if (!TryParseSdmxDate(dimValue, out var date))
                    return null;

                if (!TryParseDecimalInvariant(obsValue, out var dec))
                    return null;

                return new BanRepSeriesData { Date = date, Value = dec };
            })
            .Where(x => x is not null)
            .Cast<BanRepSeriesData>()
            .OrderBy(x => x.Date)
            .ToList();

        return obs;
    }
    
    private static string BuildDtfUrl(DateOnly? start, DateOnly? end)
    {
        // Regras obrigatorias:
        // - startPeriod/endPeriod devem conter apenas o ano (YYYY).
        // - A data de referencia e a data de entrada do dia corrente.
        // - startPeriod = ano - 1, endPeriod = ano + 1.
        var referenceDate = end ?? start ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var year = referenceDate.Year;

        var query = new List<string>
        {
            $"startPeriod={year - 1:0000}",
            $"endPeriod={year + 1:0000}",
            "dimensionAtObservation=TIME_PERIOD",
            "detail=full"
        };

        var qs = string.Join("&", query);

        return $"{AgencyId},{DtfDailyHistFlowId},{Version}/all/ALL/?{qs}";
    }
    
    private static string IsoWeekKey(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        var week = ISOWeek.GetWeekOfYear(dt);
        var year = ISOWeek.GetYear(dt);
        return $"{year:D4}-W{week:D2}";
    }

    private static bool TryParseSdmxDate(string raw, out DateOnly date)
    {
        // A doc mostra exemplo 20240816 (AAAAMMDD), mas alguns SDMX retornam yyyy-MM-dd.
        // Suportamos ambos.
        if (raw.Length == 8 && int.TryParse(raw, out _))
        {
            var y = int.Parse(raw[..4], CultureInfo.InvariantCulture);
            var m = int.Parse(raw.Substring(4, 2), CultureInfo.InvariantCulture);
            var d = int.Parse(raw.Substring(6, 2), CultureInfo.InvariantCulture);
            date = new DateOnly(y, m, d);
            return true;
        }

        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        date = default;
        return false;
    }

    private static bool TryParseDecimalInvariant(string raw, out decimal value)
    {
        // SDMX geralmente usa ponto decimal.
        // Também removemos possiveis espacos.
        raw = raw.Trim();

        return decimal.TryParse(
            raw,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static async Task<string?> SafeReadBody(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || statusCode == HttpStatusCode.InternalServerError
            || statusCode == HttpStatusCode.BadGateway
            || statusCode == HttpStatusCode.ServiceUnavailable
            || statusCode == HttpStatusCode.GatewayTimeout;
    }
}
