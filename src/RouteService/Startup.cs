using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
//using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Kerberos.NET;
using Kerberos.NET.Crypto;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pivotal.IWA.ServiceLightCore;
using ProxyKit;

namespace RouteService
{
    

    public class Startup
    {
        const string X_CF_Forwarded_Url = "X-CF-Forwarded-Url";
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
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

            services.AddProxy();
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
//            if (env.IsDevelopment())
//            {
//                app.UseDeveloperExceptionPage();
//            }

            app.UseAuthentication().ForbidAnonymous();
            
            app.RunProxy(async context =>
            {
                HttpResponseMessage response;
                if (!context.Request.Headers.TryGetValue(X_CF_Forwarded_Url, out var forwardTo))
                {
                    
                    response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent($"Required header {X_CF_Forwarded_Url} not present in the request")
                    };
                    return response;
                }
                

                var forwardContext = context.ForwardTo(forwardTo.ToString());
                
                forwardContext.UpstreamRequest.Headers.Add("X-CF-Identity", context.User.Identity.Name);
                forwardContext.UpstreamRequest.Headers.Remove("Authorization");
//                forwardContext.UpstreamRequest.Headers.GetCookies(".AspNetCore.Cookies");
//                forwardContext.HttpContext.Request.Headers["Host"] = new Uri(forwardTo.ToString()).Host;

                foreach (var header in forwardContext.UpstreamRequest.Headers)
                {
                    Console.WriteLine($"{header.Key}: {header.Value.FirstOrDefault()}");
                }
                
                response = await forwardContext.Send();
                Console.WriteLine($"Downstream responded with: {response.StatusCode}");
                foreach (var cookie in context.Response.Headers["Set-Cookie"])
                {
                    response.Headers.TryAddWithoutValidation("Set-Cookie", cookie);
                }

                return response;
            });
        }
    }
}
