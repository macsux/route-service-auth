namespace RouteServiceAuth
{
    public class LdapOptions
    {
        public string Server { get; set; }
        public int Port { get; set; } = 389;
        public string Username { get; set; }
        public string Password { get; set; }
        
        public string Filter { get; set; } = "(objectClass=group)";
        public string GroupsQuery { get; set; }
    }
}