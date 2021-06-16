using System;
using System.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.IdentityModel.Tokens;

namespace RouteServiceAuth.Proxy.Configuration
{
    [PublicAPI]
    public class SecurityKeySecret : Secret
    {
        public string? Pem { get; set; }
        public SecurityKey? GetSecurityKey()
        {
            
            if (Pem == null) throw new InvalidOperationException($"{nameof(Pem)} cannot be null");
            if (!PemEncoding.TryFind(Pem, out var pemFields))
                return null;
            var label = Pem[pemFields.Label];
            if (label.StartsWith("RSA"))
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(Pem);
                return new RsaSecurityKey(rsa);
            }

            return null;
        }
    }
}