using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using static Dibk.Ftpb.Api.Email.Provider.GraphApi.GraphApiEmailProvider;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi
{
    public static class ServiceConfigurationExtension
    {
        public static void AddGraphApiEmailProvider(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped(x => new TokenProvider(
                        configuration["GraphApiAuth:ClientId"],
                        configuration["GraphApiAuth:ClientSecret"],
                        new string[] { "https://graph.microsoft.com/.default" },
                        configuration["GraphApiAuth:TenantId"]));

            services.AddScoped<IAuthenticationProvider, ClientCredentialsAuthProvider>();
            services.AddScoped<Interfaces.IFtpbEmailProvider, GraphApiEmailProvider>();
            services.Configure<GraphApiEmailSettings>(configuration.GetSection(GraphApiEmailSettings.ConfigSection));
        }

        public static IHealthChecksBuilder AddGraphApiHealthCheck(this IHealthChecksBuilder builder)
        {
            return builder.AddCheck<GraphApiHealthCheck>("GrapApi connection check");
        }
    }
}