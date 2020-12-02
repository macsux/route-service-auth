using System.Collections.Generic;
using System.Data;
using System.Linq;
using FluentValidation;

namespace RouteServiceAuth
{
    public class KerberosProxyOptions
    {
        // public KerberosOptions Kerberos { get; set; } = new KerberosOptions();
        // public LdapOptions Ldap { get; set; } = new LdapOptions();
        public string RolesHttpHeaderName { get; set; }
        public string IdentityHttpHeaderName { get; set; }
        public string DestinationHeaderName { get; set; }
        public Dictionary<int,ProxyEntry> Ingress { get; set; } = new Dictionary<int,ProxyEntry>();
        public  Dictionary<int,ProxyEntry> Egress { get; set; } = new  Dictionary<int,ProxyEntry>();
    }
}