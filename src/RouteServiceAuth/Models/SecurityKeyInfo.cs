using Microsoft.IdentityModel.Tokens;

namespace RouteServiceAuth.Models
{
    /// <summary>
    /// Information about a security key
    /// </summary>
    public class SecurityKeyInfo
    {
        /// <summary>
        /// The key
        /// </summary>
        public SecurityKey? Key { get; set; }

        /// <summary>
        /// The signing algorithm
        /// </summary>
        public string? SigningAlgorithm { get; set; }
    }
}