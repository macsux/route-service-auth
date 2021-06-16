using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace RouteServiceAuth.Proxy.Configuration
{
    [PublicAPI]
    public abstract class Secret
    {
        public string? Id { get; set; }
    }
}