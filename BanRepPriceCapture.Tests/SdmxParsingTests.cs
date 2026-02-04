using System.Net;
using System.Net.Http.Headers;
using BanRepPriceCapture.DtfWeeklyPoc.Models;
using BanRepPriceCapture.DtfWeeklyPoc.Services;
using Xunit;

namespace BanRepPriceCapture.Tests;

public sealed class SdmxParsingTests
{
    [Fact]
    public void ParseSdmxGenericData_ParsesDatesAndValues()
    {
        using var stream = OpenFixture("SdmxParseSample.xml");

        var result = BanRepSdmxClient.ParseSdmxGenericData(stream);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2024, 8, 16), result[0].Date);
        Assert.Equal(10.751m, result[0].Value);
        Assert.Equal(new DateOnly(2024, 8, 17), result[1].Date);
        Assert.Equal(10.752m, result[1].Value);
    }

    [Fact]
    public void AggregateWeeklyByIsoWeek_ReturnsLastObservationPerWeek()
    {
        var daily = new List<BanRepSeriesData>
        {
            new() { Date = new DateOnly(2024, 12, 30), Value = 11.0m },
            new() { Date = new DateOnly(2024, 12, 31), Value = 11.1m },
            new() { Date = new DateOnly(2025, 1, 2), Value = 11.2m },
            new() { Date = new DateOnly(2025, 1, 7), Value = 11.5m }
        };

        var weekly = BanRepSdmxClient.AggregateWeeklyByIsoWeek(daily);

        Assert.Equal(2, weekly.Count);
        Assert.Equal(new DateOnly(2025, 1, 2), weekly[0].Date);
        Assert.Equal(11.2m, weekly[0].Value);
        Assert.Equal(new DateOnly(2025, 1, 7), weekly[1].Date);
        Assert.Equal(11.5m, weekly[1].Value);
    }

    [Fact]
    public async Task GetDtfDailyAsync_UsesSdmxResponseFromHttpHandler()
    {
        using var stream = OpenFixture("SdmxWeeklySample.xml");
        using var reader = new StreamReader(stream);
        var body = await reader.ReadToEndAsync();

        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            capturedRequest = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            return response;
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://totoro.banrep.gov.co/nsi-jax-ws/rest/data/") };
        var client = new BanRepSdmxClient(http);

        var daily = await client.GetDtfDailyAsync(start: new DateOnly(2026, 2, 3));

        Assert.Equal(4, daily.Count);
        Assert.Equal(new DateOnly(2024, 12, 30), daily[0].Date);
        Assert.Equal(11.0m, daily[0].Value);
        Assert.Equal(new DateOnly(2024, 12, 31), daily[1].Date);
        Assert.Equal(11.1m, daily[1].Value);
        Assert.Equal(new DateOnly(2025, 1, 2), daily[2].Date);
        Assert.Equal(11.2m, daily[2].Value);
        Assert.Equal(new DateOnly(2025, 1, 7), daily[3].Date);
        Assert.Equal(11.5m, daily[3].Value);

        var requestUri = capturedRequest?.RequestUri?.ToString();
        Assert.Equal(
            "https://totoro.banrep.gov.co/nsi-jax-ws/rest/data/ESTAT,DF_DTF_DAILY_HIST,1.0/all/ALL/?startPeriod=2025&endPeriod=2027&dimensionAtObservation=TIME_PERIOD&detail=full",
            requestUri);
    }

    [Fact]
    public async Task GetDtWeeklyAsync_ReturnsLastObservationPerIsoWeek()
    {
        using var stream = OpenFixture("SdmxWeeklySample.xml");
        using var reader = new StreamReader(stream);
        var body = await reader.ReadToEndAsync();

        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            return response;
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://totoro.banrep.gov.co/nsi-jax-ws/rest/data/") };
        var client = new BanRepSdmxClient(http);

        var weekly = await client.GetDtWeeklyAsync(start: new DateOnly(2026, 2, 3));

        Assert.Equal(2, weekly.Count);
        Assert.Equal(new DateOnly(2025, 1, 2), weekly[0].Date);
        Assert.Equal(11.2m, weekly[0].Value);
        Assert.Equal(new DateOnly(2025, 1, 7), weekly[1].Date);
        Assert.Equal(11.5m, weekly[1].Value);
    }

    private static FileStream OpenFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return File.OpenRead(path);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
