using Dibk.Ftpb.Api.Email.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SendGrid.Extensions.DependencyInjection;

namespace Dibk.Ftpb.Api.Email.Provider.SendGrid
{
    public static class ServiceConfigurationExtension
    {
        public static void AddSendGrid(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSendGrid(options =>
            {
                options.ApiKey = configuration.GetValue<string>("SendGrid:ApiKey");
            });

            services.AddScoped<IFtpbEmailProvider, SendGridProvider>();
        }
    }
}
