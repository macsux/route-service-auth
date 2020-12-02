using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProxyKit;
using Steeltoe.Management.CloudFoundry;
using Steeltoe.Management.Endpoint;

namespace SampleApp
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCloudFoundryActuators();
            services.AddProxy();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAllActuators();
                endpoints.MapGet("/", async context =>
                {
                    string identity = "Anonymous";
                    if (context.Request.Headers.TryGetValue("X-CF-Identity", out var identityVal))
                        identity = identityVal;
                    await context.Response.WriteAsync($"Identity: {identity}");

                    if (context.Request.Headers.TryGetValue("X-CF-Roles", out var rolesVal))
                    {
                        await context.Response.WriteAsync("\nRoles:\n");
                        foreach (var role in rolesVal.ToString().Split(","))
                        {
                            await context.Response.WriteAsync($"- {role}\n");
                        }
                    }
                });
                endpoints.MapGet("echo", async context =>
                {
                    // Task<HttpResponseMessage> Echo(HttpContext ctx)
                    // {
                    //     ctx.ForwardTo("http://localhost:3333").Send();
                    // }

                    await new ProxyMiddleware(null, new ProxyOptions()
                    {
                        HandleProxyRequest = ctx =>
                        {
                            var uri = "http://localhost:3333";
                            var req = ctx.ForwardTo(uri);
                            req.UpstreamRequest.RequestUri = new Uri(uri);
                            return req.Send();
                        }
                    }).Invoke(context);

                });
            });
        }
    }
}