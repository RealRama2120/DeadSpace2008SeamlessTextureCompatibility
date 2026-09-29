using System;
using System.Collections.Generic;
using System.IO;

namespace DeadSpaceTextureLauncher
{
    internal sealed class LaunchInvocation
    {
        public bool ConfigureOnly;
        public bool FromSteam;
        public bool FromEaProxy;
        public int ProxyProcessId;
        public string GameRoot = string.Empty;
        public string ForwardedGamePath = string.Empty;
        public string ForwardedArguments = string.Empty;

        public static LaunchInvocation Parse(string[] args)
        {
            LaunchInvocation result = new LaunchInvocation();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("--configure", StringComparison.OrdinalIgnoreCase) || arg.Equals("/configure", StringComparison.OrdinalIgnoreCase))
                    result.ConfigureOnly = true;
                else if (arg.Equals("--steam", StringComparison.OrdinalIgnoreCase))
                {
                    result.FromSteam = true;
                    if (i + 1 < args.Length)
                    {
                        result.ForwardedGamePath = args[++i];
                        List<string> forwarded = new List<string>();
                        while (i + 1 < args.Length) forwarded.Add(QuoteIfNeeded(args[++i]));
                        result.ForwardedArguments = string.Join(" ", forwarded.ToArray());
                    }
                }
                else if (arg.Equals("--ea-proxy", StringComparison.OrdinalIgnoreCase))
                    result.FromEaProxy = true;
                else if (arg.Equals("--proxy-pid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    int.TryParse(args[++i], out result.ProxyProcessId);
                else if (arg.Equals("--game-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    result.GameRoot = args[++i];
            }
            return result;
        }

        private static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            return value.IndexOf(' ') >= 0 ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;
        }
    }
}
