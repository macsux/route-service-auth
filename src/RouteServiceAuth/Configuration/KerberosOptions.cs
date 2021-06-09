using System;

namespace RouteServiceAuth
{
    public class KerberosOptions
    {
        public string Realm { get; set; } = Environment.GetEnvironmentVariable("REALM") ?? Environment.GetEnvironmentVariable("DOMAIN")?.ToUpper();
        public string Kdc { get; set; } = Environment.GetEnvironmentVariable("KDC")?.ToUpper();
        public string Krb5ConfigPath { get; set; }
    }
}