using BanRepPriceCapture.DtfWeeklyPoc.Jobs;
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
builder.Services.AddScoped<DtWeeklyJob>();

var app = builder.Build();

// Swagger habilitado apenas em ambiente de desenvolvimento é boa prática
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// GET /dtf-weekly?start=2023-01-01&end=2024-12-31
app.MapGet("/dtf-weekly", async (
    string? start,
    string? end,
    DtWeeklyJob job,
    CancellationToken ct) =>
{
    static DateOnly? ParseDate(string? s)
        => DateOnly.TryParse(s, out var d) ? d : null;

    var startDate = ParseDate(start);
    var endDate = ParseDate(end);

    try
    {
        var data = await job.ExecuteAsync(startDate, endDate, ct);

        return Results.Ok(new
        {
            series = "DTF 90 dias (semanal, agregado a partir do SDMX diario)",
            start = startDate,
            end = endDate,
            count = data.Count,
            data
        });
    }
    catch (TimeoutException ex)
    {
        return Results.Problem(
            title: "Timeout ao consultar BanRep SDMX",
            detail: ex.Message,
            statusCode: StatusCodes.Status504GatewayTimeout);
    }
    catch (BanRepSdmxException ex)
    {
        return Results.Problem(
            title: "Erro retornado pelo BanRep SDMX",
            detail: ex.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(
            title: "Falha de rede ao consultar BanRep SDMX",
            detail: ex.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();
