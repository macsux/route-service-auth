using System.Collections.Generic;

namespace RouteServiceAuth
{
    public class ProxyEntry 
    {
        public int ListenPort { get; set; }
        public string TargetUrl { get; set; }
        public string UserAccount { get; set; }
        public string Spn { get; set; }
        public string Password { get; set; }
        public List<Route> Routes { get; set; }
    }
}