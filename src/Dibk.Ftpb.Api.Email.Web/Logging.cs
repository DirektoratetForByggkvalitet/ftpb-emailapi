using Serilog.Events;
using Serilog;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Serilog.Debugging;
using Elastic.Apm.SerilogEnricher;

namespace Dibk.Ftpb.Email.Api;

public static class Logging
{
    public static void ConfigureLogging(IConfiguration configuration)
    {
        var elasticSearchUrl = configuration.GetValue<string>("Serilog:ConnectionUrl");
        var elasticUsername = configuration.GetValue<string>("Serilog:Username");
        var elasticPassword = configuration.GetValue<string>("Serilog:Password");
        var elasticIndexFormat = configuration.GetValue<string>("Serilog:IndexFormat");

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Debug)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Elastic.Apm", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithCorrelationIdHeader()
            .Enrich.WithElasticApmCorrelationInfo()
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss.fff} {Scope} {SourceContext} [{Level}] {ArchiveReference} {Message}{NewLine}{Exception}");

        if (!string.IsNullOrEmpty(elasticSearchUrl))
            loggerConfiguration.WriteTo.Elasticsearch(new Uri[] { new Uri(elasticSearchUrl) },
                opts => { opts.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName(elasticIndexFormat); },
                tr => { tr.Authentication(new BasicAuthentication(elasticUsername, elasticPassword)); });
        else
            Console.WriteLine("ERROR IN SERILOG CONFIGURATION - Unable to register elastic sink. URL is missing in config");

        Log.Logger = loggerConfiguration.CreateLogger();

        if (bool.TryParse(configuration["Serilog:SelfLogEnabled"], out var selflogEnabled))
            if (selflogEnabled)
                SelfLog.Enable(msg => Console.WriteLine(msg));
    }
}