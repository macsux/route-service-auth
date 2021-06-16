using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RouteServiceAuth.Proxy.Configuration.Validation
{
    public class RsaPrivateKeyValidator<TOptions> : PropertyValidator<TOptions, string?>
    {
        public override bool IsValid(ValidationContext<TOptions> context, string? value)
        {
            if (value == null)
                return false;
            var rsa = RSA.Create();
            try
            {
                // rsa.ImportRSAPrivateKey(Convert.FromBase64String(value), out _);
                rsa.ImportFromPem(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override string Name => "RSAPrivateKeyValidator";
        protected override string GetDefaultMessageTemplate(string errorCode) => "Not a valid base64 encoded RSA private key";
    }
}