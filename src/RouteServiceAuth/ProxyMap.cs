using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace RouteServiceAuth
{
    public class ProxyMap
    { 
        public Dictionary<int, ProxyMapEntry> Entries { get; set; }
        public Range ReservedPortRange { get; set; } = new Range(10000, 10100);

        public ProxyMap BindFrom(IConfiguration configuration)
        {
            configuration.GetSection("ProxyMap").Bind(this);
            var list = new List<ProxyMapEntry>();
            configuration.GetSection("ProxyMap:Entries").Bind(list);
            this.Entries = list.ToDictionary(x => x.ListenPort, x => x);
            return this;
        }
        
    }
}