using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using RouteServiceAuth.Proxy.Transformers;

namespace RouteServiceAuth.Proxy.Reverse
{
    public class ProxyForwardingPipelineBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private List<Type> _items = new();

        public ProxyForwardingPipelineBuilder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider.CreateScope().ServiceProvider;
        }

        public ProxyForwardingPipelineBuilder AddMiddleware<T>()
        {
            _items.Add(typeof(T));
            return this;
        }

        public ProxyRequestDelegate Build()
        {
            ProxyRequestDelegate next = context => Task.CompletedTask;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var middleware = (IProxyMiddleware)ActivatorUtilities.CreateInstance(_serviceProvider, _items[i], next);
                next = context => middleware.Invoke(context);
            }

            return next;
        }
    }
}