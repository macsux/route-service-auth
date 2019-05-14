using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.Configuration;
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
            var hostingEnv = new HostingEnvironment();
            var options = new WebHostOptions(new ConfigurationBuilder().AddEnvironmentVariables("ASPNETCORE_").Build());
            hostingEnv.Initialize(ResolveContentRootPath(Directory.GetCurrentDirectory(), AppContext.BaseDirectory),options);
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{hostingEnv.ContentRootPath}.json", true, true)
                .AddConfigServer()
                .Build();
            var proxyMap = new ProxyMap().BindFrom(config);
            
            return WebHost.CreateDefaultBuilder(args)
                .AddCloudFoundry()
                .AddConfigServer()
                .UseUrls(proxyMap.Entries.Select(x => $"http://0.0.0.0:{x.Key}").ToArray())
                .UseStartup<Startup>();
            
        }
        private static string ResolveContentRootPath(string contentRootPath, string basePath)
        {
            if (string.IsNullOrEmpty(contentRootPath))
                return basePath;
            if (Path.IsPathRooted(contentRootPath))
                return contentRootPath;
            return Path.Combine(Path.GetFullPath(basePath), contentRootPath);
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
