using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RouteServiceAuth.Proxy.Configuration;
using Secret = RouteServiceAuth.Proxy.Configuration.Secret;

namespace RouteServiceAuth
{
    public class CertificateStore : ISigningCredentialStore, IValidationKeysStore
    {
        private readonly IOptionsMonitor<ProxyOptions> _proxyOptions;
        private readonly IOptionsMonitor<Dictionary<string, Secret>> _secrets;

        public CertificateStore(IOptionsMonitor<ProxyOptions> proxyOptions, IOptionsMonitor<Dictionary<string, Secret>> secrets)
        {
            _proxyOptions = proxyOptions;
            _secrets = secrets;
        }

        public Task<SigningCredentials> GetSigningCredentialsAsync()
        {
            var signature = _secrets.CurrentValue!.GetValueOrDefault(_proxyOptions.CurrentValue.SigningSecurityKeyId) as SecurityKeySecret;
            var key = signature?.GetSecurityKey() ?? throw new InvalidOperationException("Signing key must be an RSA private key");
            return Task.FromResult(new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        }

        public async Task<IEnumerable<SecurityKeyInfo>> GetValidationKeysAsync()
        {
            var credentials = await GetSigningCredentialsAsync();
            return new[] {new SecurityKeyInfo {Key = credentials.Key, SigningAlgorithm = credentials.Algorithm}};
        }
    }
}