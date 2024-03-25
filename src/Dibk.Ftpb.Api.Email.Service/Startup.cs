using Dibk.Ftpb.Api.Email.Provider.GraphApi;
using Dibk.Ftpb.Api.Email.Provider.Office365;
using Elastic.Apm.NetCoreAll;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
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
            services.AddHealthChecks()
                .AddGraphApiHealthCheck();

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

            app.UseAllElasticApm(Configuration);
            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });
        }

        private void ConfigureLogging(IServiceProvider serviceProvider)
        {
            var elasticSearchUrl = Configuration["Serilog:ConnectionUrl"];
            var elasticUsername = Configuration["Serilog:Username"];
            var elasticPassword = Configuration["Serilog:Password"];
            var elasticIndexFormat = Configuration["Serilog:IndexFormat"];

            var config = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Debug)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Elastic.Apm", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithCorrelationIdHeader()
                .WriteTo.Trace(outputTemplate: "{Timestamp:HH:mm:ss.fff} {SourceContext} [{Level}] {Message}{NewLine}{Exception}")
                .WriteTo.Console();

            if (!string.IsNullOrEmpty(elasticSearchUrl))
                config.WriteTo.Elasticsearch(new Uri[] { new Uri(elasticSearchUrl) },
                                   opts => { opts.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName(elasticIndexFormat); },
                                   tr => { tr.Authentication(new BasicAuthentication(elasticUsername, elasticPassword)); });
            else
                Console.WriteLine("ERROR IN SERILOG CONFIGURATION - Unable to register elastic sink. URL is missing in config");

            Log.Logger = config.CreateLogger();
        }
    }
}