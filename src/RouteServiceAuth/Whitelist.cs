using RouteServiceAuth.Kerberos.NET;

namespace RouteServiceAuth
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public interface IWhitelist
    {
        bool IsWhitelisted(HttpRequest request);
    }

    public sealed class Whitelist : IWhitelist
    {
        const string BaseAddress = "http://localhost";

        private readonly ILogger _logger;
        private readonly IOptionsMonitor<WhitelistOptions> _optionsMonitor;

        public List<Uri> Entries { get; private set; } = new List<Uri>();

        public Whitelist(ILogger<Whitelist> logger, IOptionsMonitor<WhitelistOptions> optionsMonitor)
        {
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _optionsMonitor.OnChange(options => Bind(options));
            Bind(_optionsMonitor.CurrentValue);
        }

        void Bind(WhitelistOptions options)
        {
            var entries = new List<Uri>();
            foreach(var absolutePath in options.Paths)
            {
                var entry = CreateEntry(absolutePath);
                _logger?.LogTrace($"Adding {absolutePath} as {entry}");
                entries.Add(entry);
            }
            Entries = entries;
            _logger?.LogTrace($"Bound {entries.Count} entries to the whitelist");
        }

        public Uri CreateEntry(Uri source)
        {
            return CreateEntry(source.AbsolutePath);
        }

        public Uri CreateEntry(string absolutePath)
        {
            var uri = new Uri($"{BaseAddress}{absolutePath}");
            return uri;
        }

        public bool IsWhitelisted(HttpRequest request)
        {
            _logger?.LogDebug($"Whitelist.IsWhitelisted: {request.Path}");
            if(request.Headers.TryGetForwardAddress(out var forwardTo))
            {
                if(Uri.TryCreate(forwardTo, UriKind.Absolute, out var forwardUri))
                {
                    _logger?.LogTrace($"Checking whitelist on behalf of {forwardUri}");
                    forwardUri = CreateEntry(forwardUri);
                    if(Entries.Any(e=>e == forwardUri || (e.AbsolutePath.EndsWith('/') && e.IsBaseOf(forwardUri))))
                    {
                        _logger?.LogTrace($"{forwardUri}:true");
                        return true;
                    }
                    _logger.LogTrace($"{forwardUri}:false");
                    return false;
                }
                else
                {
                    _logger?.LogWarning("Unexpected content passed as header value; expected a valid Uri; enable tracing to view untrusted header values.");
                    _logger?.LogTrace($"Unexpected content passed as header value; expected a valid Uri; value={forwardTo};");

                    throw new Exception("Could not construct valid Uri from forward address");
                }
            }
            return false;
        }
    }
}