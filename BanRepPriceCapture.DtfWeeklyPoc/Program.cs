using BanRepPriceCapture.DtfWeeklyPoc.Jobs;
using BanRepPriceCapture.DtfWeeklyPoc.Models;
using BanRepPriceCapture.DtfWeeklyPoc.Services;

var builder = WebApplication.CreateBuilder(args);

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClient configurado para o SDMX do BanRep
builder.Services.AddHttpClient<BanRepSdmxClient>(http =>
{
    http.BaseAddress = new Uri("https://totoro.banrep.gov.co/nsi-jax-ws/rest/data/");
    http.Timeout = TimeSpan.FromSeconds(30);
    http.DefaultRequestHeaders.UserAgent.ParseAdd("BTG-DTF-Weekly-POC/.NET8");
    http.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
});

// Job
builder.Services.AddScoped<DtfDailyJob>();
builder.Services.AddScoped<DtWeeklyJob>();

var app = builder.Build();

// Swagger habilitado apenas em ambiente de desenvolvimento é boa prática
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

static DateOnly? ParseDate(string? s)
    => DateOnly.TryParse(s, out var d) ? d : null;

static IResult BuildSeriesResponse(
    string series,
    DateOnly? startDate,
    DateOnly? endDate,
    List<BanRepSeriesData> data)
{
    return Results.Ok(new DtfSeriesResponse(
        series,
        startDate,
        endDate,
        data.Count,
        data));
}

static IResult HandleException(Exception ex)
{
    return ex switch
    {
        TimeoutException timeout => Results.Problem(
            title: "Timeout ao consultar BanRep SDMX",
            detail: timeout.Message,
            statusCode: StatusCodes.Status504GatewayTimeout),
        BanRepSdmxException sdmx => Results.Problem(
            title: "Erro retornado pelo BanRep SDMX",
            detail: sdmx.Message,
            statusCode: StatusCodes.Status502BadGateway),
        HttpRequestException http => Results.Problem(
            title: "Falha de rede ao consultar BanRep SDMX",
            detail: http.Message,
            statusCode: StatusCodes.Status502BadGateway),
        _ => Results.Problem(
            title: "Erro inesperado",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError)
    };
}

// GET /dtf-daily?start=2023-01-01&end=2024-12-31
app.MapGet("/dtf-daily", async (
    [AsParameters] DtfSeriesRequest request,
    DtfDailyJob job,
    CancellationToken ct) =>
{
    var startDate = ParseDate(request.Start);
    var endDate = ParseDate(request.End);

    try
    {
        var data = await job.ExecuteAsync(startDate, endDate, ct);

        return BuildSeriesResponse(
            "DTF 90 dias (diario, direto do SDMX)",
            startDate,
            endDate,
            data);
    }
    catch (Exception ex)
    {
        return HandleException(ex);
    }
});

// GET /dtf-weekly?start=2023-01-01&end=2024-12-31
app.MapGet("/dtf-weekly", async (
    [AsParameters] DtfSeriesRequest request,
    DtWeeklyJob job,
    CancellationToken ct) =>
{
    var startDate = ParseDate(request.Start);
    var endDate = ParseDate(request.End);

    try
    {
        var data = await job.ExecuteAsync(startDate, endDate, ct);

        return BuildSeriesResponse(
            "DTF 90 dias (semanal, agregado a partir do SDMX diario)",
            startDate,
            endDate,
            data);
    }
    catch (Exception ex)
    {
        return HandleException(ex);
    }
});

app.Run();
