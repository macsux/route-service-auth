namespace RouteServiceAuth
{
    public class KerberosOptions
    {
        public string Realm { get; set; }
        public string Kdc { get; set; }
        public string Krb5ConfigPath { get; set; }
    }
}