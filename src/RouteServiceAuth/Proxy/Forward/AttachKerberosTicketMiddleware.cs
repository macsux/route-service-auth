using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentValidation;
using Kerberos.NET.Credentials;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ProxyKit;
using RouteServiceAuth.Kerberos;
using RouteServiceAuth.Proxy.Configuration;

namespace RouteServiceAuth.Proxy.Forward
{
    public class AttachKerberosTicketMiddleware : ProxyMiddlewareBase
    {
        private readonly IOptionsMonitor<Dictionary<string,Secret>> _secrets;
        private readonly KerberosClientProvider _kerberosClientProvider;
        private readonly IValidator<ProxyEntry> _entryValidator;


        public AttachKerberosTicketMiddleware(RequestDelegate next, 
            IOptionsMonitor<Dictionary<string,Secret>> secrets, 
            KerberosClientProvider kerberosClientProvider,
            IValidator<ProxyEntry> entryValidator) : base(next)
        {
            _secrets = secrets;
            _kerberosClientProvider = kerberosClientProvider;
            _entryValidator = entryValidator;
        }


        protected override async Task<HttpResponseMessage> ProxyRequest(HttpContext context)
        {
            var proxyEntry = context.GetProxyEntry();
            // ReSharper disable once MethodHasAsyncOverload
            _entryValidator.Validate(proxyEntry, opt => opt.ThrowOnFailures().IncludeRuleSets(ProxyEntry.Validator.EgressRules));
            if (!_secrets.CurrentValue.TryGetSecretOfType<WindowsCredential>(proxyEntry.CredentialsId, out var credential))
            {
                throw new InvalidOperationException($"Proxy entry {proxyEntry.Id} referenced credentials are not of type {nameof(WindowsCredential)}");
            }
            
            var kerbCredential = new KerberosPasswordCredential(credential.UserAccount, credential.Password);
            var client = _kerberosClientProvider.GetClient(credential);
            await client.Authenticate(kerbCredential);
            var uri = new Uri(proxyEntry.TargetUrl!);
            var spn = proxyEntry.Spn ?? $"http/{uri.Host}";
            var ticket = await client.GetServiceTicket(spn);
            var base64Ticket = Convert.ToBase64String(ticket.EncodeGssApi().ToArray());
            var proxyRequest = context.ForwardTo(proxyEntry.TargetUrl);
            proxyRequest.UpstreamRequest.Headers.Authorization = new AuthenticationHeaderValue("Negotiate", base64Ticket);
            return await proxyRequest.Send();
        }
    }
}