using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.GssKerberos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProxyKit;
//using System.Net.Http.Headers;

namespace RouteServiceAuth
{
    public class ProxyMapEntry
    {
        public int ListenPort { get; set; }
        public string TargetUrl { get; set; }
        public string ClientLogin { get; set; }
        public string ClientPassword { get; set; }
        public string TargetSpn { get; set; }
    }

    public class ProxyMap
    { 
        public Dictionary<int, ProxyMapEntry> Entries { get; set; }
        public Range ReservedPortRange { get; set; } = new Range(10000, 10100);

        public ProxyMap BindFrom(IConfiguration configuration)
        {
            configuration.GetSection("ProxyMap").Bind(this);
            var list = new List<ProxyMapEntry>();
            configuration.GetSection("ProxyMap:Entries").Bind(list);
            this.Entries = list.ToDictionary(x => x.ListenPort, x => x);
            return this;
        }
        
    }

    public class Range
    {
        public Range(int @from, int to)
        {
            From = @from;
            To = to;
        }

        public int From { get; set; }
        public int To { get; set; }
    }


    public class Startup
    {
        private readonly ILogger<Startup> _logger;
        public IConfiguration Configuration { get; }
//        public ProxyMap ProxyMap { get; private set; }

        const string X_CF_Forwarded_Url = "X-CF-Forwarded-Url";

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public Startup(ILogger<Startup> logger, IConfiguration configuration)
        {
            _logger = logger;
            Configuration = configuration;
            var result = configuration.AsEnumerable().ToArray();
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
            services.AddSingleton<ProxyMap>(ctx => new ProxyMap().BindFrom(Configuration));
//            services.AddOptions<ProxyMap>().Configure(opt => opt.BindFrom(Configuration));
            services.AddProxy();
        }

       

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            var features = app.ServerFeatures.ToArray();
//            if (env.IsDevelopment())
//            {
//                app.UseDeveloperExceptionPage();
//            }
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

                        EnsureTgt(proxySettings.ClientLogin);
                        string kerberosTicket;
                        using (var clientCredentials = GssCredentials.FromKeytab(proxySettings.ClientLogin, CredentialUsage.Initiate))
                        using (var initiator = new GssInitiator(credential: clientCredentials, spn: proxySettings.TargetSpn))
                        {
                            try
                            {
                                kerberosTicket = Convert.ToBase64String(initiator.Initiate(null));
                            }
                            catch(GssException exception)
                            {
                                await ctx.Response.WriteAsync($"Unable to acquire ticket for SPN {proxySettings.TargetSpn} using client identity {proxySettings.ClientLogin}");
                                await ctx.Response.WriteAsync(exception.Message);
                                return;
                            }
                        }

                        ctx.Items["KerberosTicket"] = kerberosTicket;
                        ctx.Items["ProxySettings"] = proxySettings;
                        await next();
                    });
                    map.RunProxy(async context =>
                    {
                        var kerberosTicket = (string)context.Items["KerberosTicket"];
                        var proxySettings = (ProxyMapEntry)context.Items["ProxySettings"];
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

                            var authResult =
                                await context.AuthenticateAsync(SpnegoAuthenticationDefaults.AuthenticationScheme);
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

                        await next();
                    });
                    reverseProxy.RunProxy(async context =>
                    {
                        HttpResponseMessage response;
                        if (!context.Request.Headers.TryGetValue(X_CF_Forwarded_Url, out var forwardTo))
                        {
                            response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                            {
                                Content = new StringContent(
                                    $"Required header {X_CF_Forwarded_Url} not present in the request")
                            };
                            _logger.LogDebug($"Received request without {X_CF_Forwarded_Url} header");
                            return response;
                        }


                        var forwardContext = context.ForwardTo(forwardTo.ToString());

                        forwardContext.UpstreamRequest.Headers.Add("X-CF-Identity", context.User.Identity.Name);
                        forwardContext.UpstreamRequest.Headers.Remove("Authorization");

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

        //todo: future improvement put tgt renewal on a worker thread as it's a heavy operation which right now will increase latency of the request
        private static void EnsureTgt(string principal)
        {
            var expiry = GetTgtExpiry();
            if (expiry < DateTime.Now)
            {
                ObtainTgt(principal);
            }

        }

        private static DateTime GetTgtExpiry()
        {
            //todo: klist exists in c:\windows\system32 which is totally different and will be defaulted to if not in bin folder
            try
            {
//                var klistResult = RunCmd("klist", null);
                var klistResult = RunCmd(@"C:\Program Files\MIT\Kerberos\bin\klist", null);
                var tgtExpiryMatch = Regex.Match(klistResult, ".{17}(?=  krbtgt)");
                if (tgtExpiryMatch.Success && DateTime.TryParse(tgtExpiryMatch.Value, out var expiry))
                    return expiry;
            }
            catch (Exception)
            {
                // cache might not even be created yet
            }

            return DateTime.MinValue;
        }

        private static void ObtainTgt(string principal)
        {
//            RunCmd("kinit", $"-k -i {principal}");
            RunCmd(@"C:\Program Files\MIT\Kerberos\bin\kinit.exe", $"-k -i {principal}");

        }


        private static string RunCmd(string cmd, string args)
        {
//            if (!ExistsOnPath(cmd)) throw new FileNotFoundException("Cannot initialize TGT - kinit.exe is not found", "kinit.exe");
            var cmdsi = new ProcessStartInfo(cmd);
            cmdsi.Arguments = args;
            cmdsi.RedirectStandardOutput = true;
            cmdsi.RedirectStandardError = true;
            var proc = Process.Start(cmdsi);
            var result = proc.StandardOutput.ReadToEnd();
            var err = proc.StandardError.ReadToEnd();

            if (!string.IsNullOrWhiteSpace(err)) throw new Exception(err);
            proc.WaitForExit();
            return result;
        }

        private static bool ExistsOnPath(string fileName)
        {
            return GetFullPath(fileName) != null;
        }

        private static string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(';'))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }
    }

}
