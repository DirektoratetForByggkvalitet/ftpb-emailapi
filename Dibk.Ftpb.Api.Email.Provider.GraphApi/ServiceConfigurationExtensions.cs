using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi
{
    public static class ServiceConfigurationExtension
    {
        public static void AddGraphApiEmailProvider(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IAuthenticationProvider>(x =>  
                new ClientCredentialsAuthProvider(
                        configuration["GraphApi:ClientId"], 
                        configuration["GraphApi:ClientSecret"], 
                        new string[] { "api://arkitektumSP/.default", "api://arkitektumSP/Mail.Send" }, 
                        configuration["GraphApi:TenantId"]));
            services.AddScoped<Interfaces.IFtpbEmailProvider, GraphApiEmailProvider>();            
        }
    }
}
