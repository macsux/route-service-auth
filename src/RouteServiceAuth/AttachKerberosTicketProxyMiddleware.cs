using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Kerberos.NET.Client;
using Kerberos.NET.Credentials;
using Microsoft.AspNetCore.Http;
using ProxyKit;

namespace RouteServiceAuth
{
    public class AttachKerberosTicketProxyMiddleware : ProxyMiddlewareBase
    {
        private readonly KerberosClient _client;

        public AttachKerberosTicketProxyMiddleware(RequestDelegate next, KerberosClient client) : base(next)
        {
            _client = client;
        }

        protected override async Task<HttpResponseMessage> ProxyRequest(HttpContext context)
        {
            var proxySettings = context.GetProxyEntry();

            var kerbCred = new KerberosPasswordCredential(proxySettings.UserAccount, proxySettings.Password);
            await _client.Authenticate(kerbCred);
            var uri = new Uri(proxySettings.TargetUrl);
            var spn = proxySettings.Spn ?? $"{uri.Scheme}/{uri.Host}";
            var ticket = await _client.GetServiceTicket(spn);
            var base64Ticket = Convert.ToBase64String(ticket.EncodeApplication().ToArray());
            var proxyRequest = context.ForwardTo(proxySettings.TargetUrl);
            proxyRequest.UpstreamRequest.Headers.Authorization = new AuthenticationHeaderValue("Negotiate", base64Ticket);
            return await proxyRequest.Send();
        }
    }
}