using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Kerberos.NET.Client;
using Kerberos.NET.Configuration;
using Kerberos.NET.Credentials;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ProxyKit;

namespace RouteServiceAuth
{
    public class AttachKerberosTicketProxyMiddleware : ProxyMiddlewareBase
    {
        private readonly IOptionsMonitor<Credential> _credentials;
        private readonly IOptionsMonitor<KerberosOptions> _kerberosOptions;
        private readonly KerberosClientProvider _kerberosClientProvider;

        // ConcurrentDictionary<string, KerberosClient> _clientsCache = new();

        public AttachKerberosTicketProxyMiddleware(RequestDelegate next, 
            IOptionsMonitor<Credential> credentials, 
            IOptionsMonitor<KerberosOptions> kerberosOptions,
            KerberosClientProvider kerberosClientProvider) : base(next)
        {
            _credentials = credentials;
            _kerberosOptions = kerberosOptions;
            _kerberosClientProvider = kerberosClientProvider;
            // _kerberosOptions.OnChange(options => _clientsCache.Clear());
        }

        // private KerberosClient CreateKerberosClient()
        // {
        //     var client = new KerberosClient();
        //     client.PinKdc(_kerberosOptions.CurrentValue.Realm, _kerberosOptions.CurrentValue.Kdc);
        //     return client;
        // }
        protected override async Task<HttpResponseMessage> ProxyRequest(HttpContext context)
        {
            var proxySettings = context.GetProxyEntry();
            var credential = _credentials.Get(proxySettings.CredentialsId);
            var kerbCredential = new KerberosPasswordCredential(credential.UserAccount, credential.Password);
            var client = _kerberosClientProvider.GetClient(credential);
            await client.Authenticate(kerbCredential);
            var uri = new Uri(proxySettings.TargetUrl);
            var spn = proxySettings.Spn ?? $"http/{uri.Host}";
            var ticket = await client.GetServiceTicket(spn);
            var base64Ticket = Convert.ToBase64String(ticket.EncodeGssApi().ToArray());
            var proxyRequest = context.ForwardTo(proxySettings.TargetUrl);
            proxyRequest.UpstreamRequest.Headers.Authorization = new AuthenticationHeaderValue("Negotiate", base64Ticket);
            return await proxyRequest.Send();
        }
    }
}