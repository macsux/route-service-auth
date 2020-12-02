// using System;
// using System.Text.Encodings.Web;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Authentication;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
//
// namespace RouteServiceAuth
// {
//     public class SpnegoProxyAuthenticationHandler : AuthenticationHandler<SpnegoProxyAuthenticationOptions>
//     {
//         private readonly ILoggerFactory _logger;
//         private readonly Func<int, SpnegoAuthenticator> _authenticatorFactory;
//
//         public SpnegoProxyAuthenticationHandler(IOptionsMonitor<SpnegoProxyAuthenticationOptions> options,
//             ILoggerFactory logger,
//             UrlEncoder encoder,
//             ISystemClock clock, Func<int, SpnegoAuthenticator> authenticatorFactory) : base(options,
//             logger,
//             encoder,
//             clock)
//         {
//             _logger = logger;
//             _authenticatorFactory = authenticatorFactory;
//         }
//
//         protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
//         {
//             var port = Context.Connection.LocalPort;
//             var authenticator = _authenticatorFactory(port);
//             var handler = new SpnegoAuthenticationHandler(OptionsMonitor, _logger, UrlEncoder, Clock, authenticator);
//             await handler.InitializeAsync(Scheme, Context);
//             return await handler.AuthenticateAsync();
//         }
//     }
// }