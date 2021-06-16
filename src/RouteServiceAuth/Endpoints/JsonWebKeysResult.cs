using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RouteServiceAuth.Models;
using RouteServiceAuth.Proxy;

namespace RouteServiceAuth.Endpoints
{
    /// <summary>
    /// Result for the jwks document
    /// </summary>
    public class JsonWebKeysResult
    {
        /// <summary>
        /// Gets the web keys.
        /// </summary>
        /// <value>
        /// The web keys.
        /// </value>
        public IEnumerable<JsonWebKey> WebKeys { get; }

        /// <summary>
        /// Gets the maximum age.
        /// </summary>
        /// <value>
        /// The maximum age.
        /// </value>
        public int? MaxAge { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonWebKeysResult" /> class.
        /// </summary>
        /// <param name="webKeys">The web keys.</param>
        /// <param name="maxAge">The maximum age.</param>
        public JsonWebKeysResult(IEnumerable<JsonWebKey> webKeys, int? maxAge)
        {
            WebKeys = webKeys ?? throw new ArgumentNullException(nameof(webKeys));
            MaxAge = maxAge ??  60 * 60;
        }

        /// <summary>
        /// Executes the result.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns></returns>
        public Task ExecuteAsync(HttpContext context)
        {


            return context.Response.WriteJsonAsync(new { keys = WebKeys }, "application/json; charset=UTF-8");
        }
    }
}