namespace Dibk.Ftpb.Api.Email.Provider.Office365
{
    public class Office365EmailSettings
    {
        public static string ConfigSection => "Office365Settings";
        public string Username { get; set; }
        public string Password { get; set; }
        public string DefaultFromAddress { get; set; }
        public string DefaultFromDisplayName { get; set; }
    }
}
