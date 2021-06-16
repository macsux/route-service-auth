using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
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
            services
                .AddAuthentication(opt =>
                {
                    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opt =>
                {
                    opt.TokenValidationParameters = new TokenValidationParameters()
                    {
                        RequireAudience = false,
                        ValidateAudience = false,
                        ValidateActor = false,
                        ValidateIssuer = false,
                        ValidateLifetime = false
                    };
                    opt.MetadataAddress = "http://localhost:8081/.well-known/openid-configuration";
                    opt.RequireHttpsMetadata = false;
                    // opt.Configuration = new OpenIdConnectConfiguration()
                    // {
                    //     JwksUri = "http://localhost:8080/token_keys"
                    // };
                });

            services.AddAuthorization(opt => opt.AddPolicy("jwt", policy => policy.RequireAuthenticatedUser()));
            
            services.AddHttpContextAccessor();
            services.AddCloudFoundryActuators();
            services.AddControllers();
            services.AddProxy();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseAuthentication();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAllActuators();
                endpoints.MapControllers();
              
                endpoints.MapGet("echo", async context =>
                {
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