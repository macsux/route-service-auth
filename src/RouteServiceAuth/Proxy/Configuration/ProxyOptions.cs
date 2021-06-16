using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.IdentityModel.Tokens;
using RouteServiceAuth.Proxy.Configuration.Validation;

namespace RouteServiceAuth.Proxy.Configuration
{
    [PublicAPI]
    public class ProxyOptions
    {
        /// <summary>
        /// Http header name used to propagate roles of authenticated principal to ingress destination
        /// </summary>
        public string RoleHttpHeaderName { get; set; } = KnownHeaders.X_CF_Roles;

        /// <summary>
        /// Http header name used to propagate the name of authenticated principal to ingress destination
        /// </summary>
        public string IdentityHttpHeaderName { get; set; } = KnownHeaders.X_CF_Identity;

        /// <summary>
        /// Http header used to determine the destination to which request should be forwarded. This is used when
        /// the forwarding destination is selected externally by another piece of ingress infrastructure middleware
        /// such as Cloud Foundry GoRouter and the proxy is acting as a route service 
        /// </summary>
        public string DestinationHeaderName { get; set; } = KnownHeaders.X_CF_Forwarded_Url;
        /// <summary>
        /// Proxy ingress route configurations
        /// </summary>
        public List<ProxyEntry> Ingress { get; set; } = new ();
        /// <summary>
        /// Proxy egress route configurations
        /// </summary>
        public  List<ProxyEntry> Egress { get; set; } = new  ();

        /// <summary>
        /// Determines how the security principal (identity and roles) established by authentication handler will be forwarded to to the target
        /// </summary>
        public PrincipalForwardingMode PrincipalForwardingMode { get; set; } = PrincipalForwardingMode.Headers;

        public string? SigningSecurityKeyId { get; set; }

        

        public class Validator : AbstractValidator<ProxyOptions>
        {
            public Validator()
            {
                RuleFor(x => x.SigningSecurityKeyId).NotEmpty();
                RuleFor(x => x.SigningSecurityKeyId).ReferencesValidCredentials().IsSecretOfType<ProxyOptions,SecurityKeySecret>();
            }
        }
    }
}