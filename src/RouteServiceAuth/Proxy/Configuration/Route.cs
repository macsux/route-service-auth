using System.Collections.Generic;
using JetBrains.Annotations;

namespace RouteServiceAuth.Proxy.Configuration
{
    [PublicAPI]
    public class Route
    {
        public static Route Default => new()
        {
            Id = "Default",
            PolicyName = AuthorizationPolicies.RequireAuthenticatedUser,
            Path = "/**"
        };
        private string? _id;

        public string Id
        {
            get => _id ?? $"[Path={Path},PolicyName={PolicyName}]";
            set => _id = value;
        }

        public HashSet<string> Methods { get; set; } = new();
        public string Path { get; set; } = "/**";
        public string? PolicyName { get; set; }
    }
}