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
            services.AddScoped<IAuthenticationProvider>(x =>  
                new ClientCredentialsAuthProvider(
                        configuration["GraphApiAuth:ClientId"], 
                        configuration["GraphApiAuth:ClientSecret"], 
                        new string[] { "https://graph.microsoft.com/.default" }, 
                        configuration["GraphApiAuth:TenantId"]));
            services.AddScoped<Interfaces.IFtpbEmailProvider, GraphApiEmailProvider>();
            services.Configure<GraphApiEmailSettings>(configuration.GetSection(GraphApiEmailSettings.ConfigSection));
        }
    }
}
