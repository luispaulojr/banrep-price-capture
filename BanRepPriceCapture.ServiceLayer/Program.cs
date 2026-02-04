using AWS.Logger.AspNetCore;
using BanRepPriceCapture.ServiceLayer.DependencyInjection;
using BanRepPriceCapture.ServiceLayer.Presentation.Endpoints;
using BanRepPriceCapture.ServiceLayer.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.global.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.uat.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.prod.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Logging.AddAWSProvider(builder.Configuration.GetAWSLoggingConfigSection());

builder.Services.AddServiceLayer(builder.Configuration);

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
