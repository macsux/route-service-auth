using JetBrains.Annotations;
// ReSharper disable InconsistentNaming

namespace RouteServiceAuth.Models
{
    [PublicAPI]
    public class JsonWebKey
    {
        public string? kty { get; set; }
        public string? use { get; set; }
        public string? kid { get; set; }
        public string? x5t { get; set; }
        public string? e { get; set; }
        public string? n { get; set; }
        public string[]? x5c { get; set; }
        public string? alg { get; set; }

        public string? x { get; set; }
        public string? y { get; set; }
        public string? crv { get; set; }
    }
}