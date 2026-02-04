using Amazon.Extensions.NETCore.Setup;
using AWS.Logger.AspNetCore;
using BanRepPriceCapture.Dtf.Application.DependencyInjection;
using BanRepPriceCapture.Dtf.Infrastructure.DependencyInjection;
using BanRepPriceCapture.Dtf.Presentation.Endpoints;
using BanRepPriceCapture.Dtf.Presentation.Middleware;
using BanRepPriceCapture.Dtf.Shared.Logging;

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