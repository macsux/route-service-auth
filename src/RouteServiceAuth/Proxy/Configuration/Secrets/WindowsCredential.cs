using System.Linq;
using FluentValidation;
using JetBrains.Annotations;

namespace RouteServiceAuth.Proxy.Configuration
{
    /// <summary>
    /// Active directory credentials
    /// </summary>
    [PublicAPI]
    public class WindowsCredential : Secret
    {
        public const string DefaultCredentialId = "ThisApp";

        /// <summary>
        /// Must be specified in username@domain.com
        /// </summary>
        public string? UserAccount { get; set; }
        /// <summary>
        /// Account password
        /// </summary>
        public string? Password { get; set; }
        /// <summary>
        /// Domain name (extracted from <see cref="UserAccount"/>)
        /// </summary>
        public string? Domain => UserAccount?.Split("@").Skip(1).FirstOrDefault();
        /// <summary>
        /// Username portion of the <see cref="UserAccount"/>
        /// </summary>
        public string? Username => UserAccount?.Split("@").FirstOrDefault();
        
        public class Validator : AbstractValidator<WindowsCredential>
        {
            public Validator()
            {
                RuleFor(x => x.UserAccount).NotEmpty().EmailAddress();
                RuleFor(x => x.Password).NotEmpty();
            }
        }
    }
}