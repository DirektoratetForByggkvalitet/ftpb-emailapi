using Dibk.Ftpb.Api.Email.Provider.GraphApi;
using Dibk.Ftpb.Email.Api;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Most settings come from the shared App Configuration store; app settings still override.
builder.Configuration.AddEmailApiConfiguration(builder.Environment.EnvironmentName);

// Add services to the container.
builder.Services.AddHealthChecks().AddGraphApiHealthCheck();

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddSerilog();
});

builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddGraphApiEmailProvider(builder.Configuration);
builder.Services.AddAllElasticApm();

var app = builder.Build();

if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
Logging.ConfigureLogging(app.Configuration);
app.UseSerilogRequestLogging();
// No in-container HTTPS redirect: the container serves plain HTTP on 8080 and TLS is
// terminated by App Service (httpsOnly). X-Forwarded-* headers preserve the original scheme.

app.UseAuthorization();

app.MapControllers();

// Liveness: is the process up? No dependencies, because App Service's health check recycles
// instances on sustained failure — this is the path configured as healthCheckPath.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });

// Readiness: can we actually reach Microsoft Graph? For monitoring and manual verification.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(ServiceConfigurationExtension.ReadyTag)
});

app.Run();