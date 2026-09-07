using Dibk.Ftpb.Api.Email.Provider.GraphApi;
using Dibk.Ftpb.Email.Api;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(ServiceConfigurationExtension.ReadyTag)
});

app.Run();