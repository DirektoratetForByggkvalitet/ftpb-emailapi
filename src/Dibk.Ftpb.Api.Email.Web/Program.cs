using Dibk.Ftpb.Api.Email.Provider.GraphApi;
using Dibk.Ftpb.Email.Api;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
app.MapHealthChecks("/health");

app.Run();