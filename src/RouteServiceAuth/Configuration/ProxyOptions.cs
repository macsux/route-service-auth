using System.Collections.Generic;
using System.Data;
using System.Linq;
using FluentValidation;

namespace RouteServiceAuth
{
    public class ProxyOptions
    {
        /// <summary>
        /// Http header name used to propagate roles of authenticated principal to ingress destination
        /// </summary>
        public string RolesHttpHeaderName { get; set; }
        /// <summary>
        /// Http header name used to propagate the name of authenticated principal to ingress destination
        /// </summary>
        public string IdentityHttpHeaderName { get; set; }
        /// <summary>
        /// Http header used to determine the destination to which request should be forwarded. This is used when
        /// the forwarding destination is selected externally by another piece of ingress infrastructure middleware
        /// such as Cloud Foundry GoRouter and the proxy is acting as a route service 
        /// </summary>
        public string DestinationHeaderName { get; set; }
        /// <summary>
        /// A mapping of Kerberos credentials to their reference IDs. 
        /// </summary>
        public Dictionary<string, Credential> Credentials { get; set; }
        /// <summary>
        /// Proxy ingress route configurations
        /// </summary>
        public List<ProxyEntry> Ingress { get; set; } = new ();
        /// <summary>
        /// Proxy egress route configurations
        /// </summary>
        public  List<ProxyEntry> Egress { get; set; } = new  ();
    }

    public record Credential
    {
        public string UserAccount { get; set; }
        public string Password { get; set; }
    }

}