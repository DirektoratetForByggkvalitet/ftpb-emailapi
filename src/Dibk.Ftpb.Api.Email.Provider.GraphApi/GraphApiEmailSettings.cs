namespace Dibk.Ftpb.Api.Email.Provider.GraphApi;

public partial class GraphApiEmailProvider
{
    public class GraphApiEmailSettings
    {
        public static string ConfigSection => "GraphApiEmailSettings";
        public string UserPrincipalName { get; set; }
        public string DefaultFromAddress { get; set; }
        public string DefaultFromDisplayName { get; set; }
    }
}