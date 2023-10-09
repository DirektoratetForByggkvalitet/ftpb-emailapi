using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dibk.Ftpb.Api.Email.Provider.Office365
{
    public static class ServiceConfigurationExtension
    {
        public static void AddOffice365EmailProvider(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<Interfaces.IFtpbEmailProvider, Office365EmailProvider>();
            //Add options
            services.Configure<Office365EmailSettings>(configuration.GetSection(Office365EmailSettings.ConfigSection));
        }
    }
}
