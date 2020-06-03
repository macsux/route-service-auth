using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using Kerberos.NET.Client;
using Kerberos.NET.Credentials;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProxyKit;
using RouteServiceAuth.Kerberos.NET;

namespace RouteServiceAuth
{
    public class Startup
    {
        private readonly ILogger<Startup> _logger;

        private readonly IConfiguration _configuration;

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public Startup(ILogger<Startup> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(SpnegoAuthenticationDefaults.AuthenticationScheme)
                .AddSpnego(options =>
                {
                    _configuration.Bind(options);
                });
//                .AddScheme<TestHeaderAuthenticationOptions, TestHeaderAuthenticationHandler>(SpnegoAuthenticationDefaults.AuthenticationScheme, _ => { })
            // .AddCookie();

            services.AddSingleton<KerberosAuthenticationEvents>();
            services.AddSingleton<ProxyMap>(ctx => new ProxyMap().BindFrom(_configuration));
            services.AddWhitelist(_configuration);
            services.AddProxy();
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            var whitelist = app.ApplicationServices.GetRequiredService<IWhitelist>();

            var features = app.ServerFeatures.ToArray();

            app.MapWhen(context => context.Request.HttpContext.Connection.LocalPort > 10000
                , map =>
                {
                    map.Use(async (ctx, next) =>
                    {
                        var proxyMap = ctx.RequestServices.GetService<ProxyMap>();
                        if (!proxyMap.Entries.TryGetValue(ctx.Connection.LocalPort, out var proxySettings))
                        {
                            await ctx.Response.WriteAsync("No proxy settings found for this port mapping");
                            return;
                        }

                        var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
                        var client = new KerberosClient(config.GetValue<string>("Kerberos:Kdc"));
                        var kerbCred = new KerberosPasswordCredential(proxySettings.ClientLogin, proxySettings.ClientPassword);
                        await client.Authenticate(kerbCred);
                        var kerberosTicket = await client.GetServiceTicket(proxySettings.TargetSpn);


                        ctx.Items["KerberosTicket"] = Convert.ToBase64String(kerberosTicket.EncodeApplication().ToArray());
                        ctx.Items["ProxySettings"] = proxySettings;
                        await next();
                    });
                    map.RunProxy(async context =>
                    {
                        var kerberosTicket = (string) context.Items["KerberosTicket"];
                        var proxySettings = (ProxyMapEntry) context.Items["ProxySettings"];
                        var proxyRequest = context.ForwardTo(proxySettings.TargetUrl);
                        proxyRequest.HttpContext.Request.Headers.Add("Authorization", $"Negotiate {kerberosTicket}");
                        return await proxyRequest.Send();
                    });
                });
            app.MapWhen(context => context.Request.HttpContext.Connection.LocalPort < 10000
                , reverseProxy =>
                {
                    reverseProxy.UseAuthentication();
                    reverseProxy.Use(async (context, next) =>
                    {
                        if (!context.User.Identity.IsAuthenticated)
                        {
                            if (whitelist.IsWhitelisted(context.Request))
                            {
                                _logger.LogDebug($"Allowing passthrough for whitelisted request {context.Request.Path}");
                            }
                            else
                            {
                                var authResult = await context.AuthenticateAsync(SpnegoAuthenticationDefaults.AuthenticationScheme);
                                if (authResult.Succeeded)
                                {
                                    _logger.LogDebug($"User {authResult.Principal.Identity.Name} successfully logged in");
                                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                                        authResult.Principal);
                                    context.User = authResult.Principal;
                                }
                                else
                                {
                                    _logger.LogDebug("User authentication failed, issuing WWW-Authenticate challenge");
                                    await context.ChallengeAsync(SpnegoAuthenticationDefaults.AuthenticationScheme
                                        , new AuthenticationProperties());
                                    return;
                                }
                            }
                        }

                        await next();
                    });
                    reverseProxy.RunProxy(async context =>
                    {
                        HttpResponseMessage response;
                        if (!context.Request.Headers.TryGetForwardAddress(out var forwardTo))
                        {
                            response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                            {
                                Content = new StringContent($"Required header {Constants.X_CF_Forwarded_Url} not present in the request")
                            };
                            _logger.LogDebug($"Received request without {Constants.X_CF_Forwarded_Url} header");
                            return response;
                        }

                        var forwardContext = context.ForwardTo(forwardTo);
                        forwardContext.UpstreamRequest.RequestUri = new Uri(forwardTo);

                        if (whitelist.IsWhitelisted(context.Request))
                        {
                            _logger.LogInformation($"Allowing passthrough for whitelisted request {forwardTo}");
                        }
                        else
                        {
                            if (context.User.Identity.IsAuthenticated)
                            {
                                forwardContext.UpstreamRequest.Headers.Add(Constants.X_CF_Identity, context.User.Identity.Name);
                                var roles = string.Join(",", context.User.Claims.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value));
                                if (!string.IsNullOrEmpty(roles))
                                {
                                    forwardContext.UpstreamRequest.Headers.Add(Constants.X_CF_Roles, roles);
                                }
                            }

                            forwardContext.UpstreamRequest.Headers.Remove("Authorization");
                        }

                        _logger.LogTrace("Headers sent downstream");
                        _logger.LogTrace("-----------------------");
                        foreach (var header in forwardContext.UpstreamRequest.Headers)
                        {
                            _logger.LogTrace($"  {header.Key}: {header.Value.FirstOrDefault()}");
                        }

                        response = await forwardContext.Send();
                        _logger.LogDebug($"Downstream responded with: {response.StatusCode}");

                        // merge cookie header set at the proxy level with headers from downstream request 
                        foreach (var cookie in context.Response.Headers["Set-Cookie"])
                        {
                            response.Headers.TryAddWithoutValidation("Set-Cookie", cookie);
                        }

                        return response;
                    });
                });
        }
    }
}