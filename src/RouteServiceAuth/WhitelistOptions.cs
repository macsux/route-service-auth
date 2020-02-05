using System;
using System.Collections.Generic;
namespace RouteServiceAuth
{
    public class WhitelistOptions
    {
        public WhitelistOptions()
        {
        }

        public List<string> Paths { get; private set; } = new List<string>();
    }
}
