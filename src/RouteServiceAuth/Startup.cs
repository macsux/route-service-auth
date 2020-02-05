using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProxyKit;
//using System.Net.Http.Headers;

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
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddSpnego()
//                .AddScheme<TestHeaderAuthenticationOptions, TestHeaderAuthenticationHandler>(SpnegoAuthenticationDefaults.AuthenticationScheme, _ => { })
                .AddCookie();
//                .AddCookie(opt =>
//                {
//                    opt.EventsType = typeof(KerberosAuthenticationEvents);
//                });
            services.AddSingleton<KerberosAuthenticationEvents>();
            services.AddWhitelist(_configuration);
            services.AddProxy();
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
//            if (env.IsDevelopment())
//            {
//                app.UseDeveloperExceptionPage();
//            }

            var whitelist = app.ApplicationServices.GetRequiredService<IWhitelist>();
            app.UseAuthentication();
            app.Use(async (context, next) =>
            {
                if (!context.User.Identity.IsAuthenticated)
                {
                    if(whitelist.IsWhitelisted(context.Request))
                    {
                        _logger.LogInformation($"Allowing passthrough for whitelisted request {context.Request.Path}");
                    }
                    else
                    {
                        var authResult = await context.AuthenticateAsync(SpnegoAuthenticationDefaults.AuthenticationScheme);
                        if (authResult.Succeeded)
                        {
                            _logger.LogDebug($"User {authResult.Principal.Identity.Name} successfully logged in");
                            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authResult.Principal);
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
            app.RunProxy(async context =>
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
                
                if(whitelist.IsWhitelisted(context.Request))
                {
                    _logger.LogInformation($"Allowing passthrough for whitelisted request {forwardTo}");
                }
                else
                {
                    forwardContext.UpstreamRequest.Headers.Add("X-CF-Identity", context.User.Identity.Name);
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
        }
    }
}
