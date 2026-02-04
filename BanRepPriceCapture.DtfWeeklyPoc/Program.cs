using Amazon.Extensions.NETCore.Setup;
using Amazon.Logger.AspNetCore;
using BanRepPriceCapture.DtfWeeklyPoc.Application.DependencyInjection;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.DependencyInjection;
using BanRepPriceCapture.DtfWeeklyPoc.Presentation.Endpoints;
using BanRepPriceCapture.DtfWeeklyPoc.Presentation.Middleware;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.global.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.uat.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.prod.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddLogging(builder.Configuration);
builder.Logging.AddAWSProvider(builder.Configuration.GetAWSLoggingConfigSection());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Swagger habilitado apenas em ambiente de desenvolvimento é boa prática
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<FlowIdMiddleware>();
app.MapDtfEndpoints();

app.Run();
