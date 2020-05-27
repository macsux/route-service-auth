using System.IO;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Steeltoe.Extensions.Configuration.CloudFoundry;
using Steeltoe.Extensions.Configuration.ConfigServer;

namespace RouteServiceAuth
{
    public class Program
    {
        public static void Main(string[] args)
        {

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            var webHost = WebHost.CreateDefaultBuilder(args)
                .AddConfigServer()
                .UseStartup<Startup>()
                .Build();
            var config = webHost.Services.GetRequiredService<IConfiguration>();
            var proxyMap = new ProxyMap().BindFrom(config);

            return WebHost.CreateDefaultBuilder(args)
                .AddCloudFoundry()
                .AddConfigServer()
                .UseUrls(proxyMap.Entries.Select(x => $"http://0.0.0.0:{x.Key}").ToArray())
                .UseStartup<Startup>();
            
        }

    }

    public static class WebHostBuilderExtensions
    {
        public static IWebHostBuilder WithPortRange(this IWebHostBuilder builder, int from, int to)
        {
            
            return builder.UseUrls(Enumerable.Range(from, to-from).Select(port => $"http://0.0.0.0:{port}").ToArray());
        }
    }
}
