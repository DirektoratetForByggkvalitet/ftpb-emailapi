using Dibk.Ftpb.Api.Email.Provider.GraphApi;
using Dibk.Ftpb.Api.Email.Provider.Office365;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using System;

namespace Dibk.Ftpb.Api.Email
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry(Configuration.GetValue<string>("ApplicationInsights:InstrumentationKey"));
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog();
            });
            services.AddHttpContextAccessor();
            services.AddControllers();

            var emailProvider = Configuration["EmailProvider"];

            if (emailProvider.Equals("GraphApi", StringComparison.OrdinalIgnoreCase))
                services.AddGraphApiEmailProvider(Configuration);
            else if (emailProvider.Equals("Office365", StringComparison.OrdinalIgnoreCase))
                services.AddOffice365EmailProvider(Configuration);
            else
                throw new Exception($"Unable to configure email provider for setting {emailProvider}");

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            ConfigureLogging(app.ApplicationServices);
            app.UseSerilogRequestLogging();
            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void ConfigureLogging(IServiceProvider serviceProvider)
        {
            var elasticSearchUrl = Configuration["Serilog:Url"];
            var elasticUsername = Configuration["Serilog:Username"];
            var elasticPassword = Configuration["Serilog:Password"];
            var elasticIndexFormat = Configuration["Serilog:IndexFormat"];

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Debug)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithCorrelationIdHeader()
                .WriteTo.Trace(outputTemplate: "{Timestamp:HH:mm:ss.fff} {SourceContext} [{Level}] {Message}{NewLine}{Exception}")
                .WriteTo.Console()
                .WriteTo.ApplicationInsights(serviceProvider.GetRequiredService<TelemetryConfiguration>(), TelemetryConverter.Traces)
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticSearchUrl))
                {
                    DetectElasticsearchVersion = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                    AutoRegisterTemplate = true,
                    ModifyConnectionSettings = x => x.BasicAuthentication(elasticUsername, elasticPassword),
                    IndexFormat = elasticIndexFormat,
                    TypeName = null
                }).CreateLogger();
        }
    }
}
