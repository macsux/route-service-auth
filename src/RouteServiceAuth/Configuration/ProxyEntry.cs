using System.Collections.Generic;

namespace RouteServiceAuth
{
    public class ProxyEntry 
    {
        public int ListenPort { get; set; }
        public string TargetUrl { get; set; }
        public string CredentialsId { get; set; }
        public string Spn { get; set; }
        public List<Route> Routes { get; set; }
    }
}