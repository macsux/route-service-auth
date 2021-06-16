using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RouteServiceAuth.Proxy.Route
{
    /// <summary>
    /// Represents a class which matches paths using ant-style path matching.
    /// </summary>
    [DebuggerDisplay("Pattern = {" + nameof(_regex) + "}")]
    public class Ant
    {
        private readonly string _pattern;
        private readonly Regex _regex;

        /// <summary>
        /// Initializes a new <see cref="Ant"/>.
        /// </summary>
        /// <param name="pattern">Ant-style pattern.</param>
        public Ant(string pattern)
        {
            _pattern = pattern ?? string.Empty;
            _regex = new Regex(EscapeAndReplace(_pattern), RegexOptions.Singleline);
        }

        /// <summary>
        /// Validates whether the input matches the given pattern.
        /// </summary>
        /// <param name="input">Path for which to check if it matches the ant-pattern.</param>
        /// <returns>Whether the input matches the pattern.</returns>
        /// <inheritdoc/>
        public bool IsMatch(string input)
        {
            input ??= string.Empty;
            return _regex.IsMatch(GetUnixPath(input));
        }

        private static string EscapeAndReplace(string pattern)
        {
            var unix = GetUnixPath(pattern);

            if (unix.EndsWith("/"))
            {
                unix += "**";
            }

            pattern = Regex.Escape(unix)
                .Replace(@"/\*\*/", "(.*[/])")
                .Replace(@"\*\*/", "(.*)")
                .Replace(@"/\*\*", "(.*)")
                .Replace(@"\*", "([^/]*)")
                .Replace(@"\?", "(.)")
                .Replace(@"}", ")")
                .Replace(@"\{", "(")
                .Replace(@",", "|");

            return $"^{pattern}$";
        }

        private static string GetUnixPath(string txt) => txt.Replace(@"\", "/").TrimStart('/');

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString() => _pattern;
    }
}