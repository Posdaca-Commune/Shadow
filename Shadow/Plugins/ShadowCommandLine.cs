using System;
using System.Collections.Generic;

namespace Shadow.Plugins;

internal sealed class ShadowCommandLine
{
    private const string CommandOption = "shadow-command";
    private const string CommandAlias = "command";

    private ShadowCommandLine(string? command, IReadOnlyDictionary<string, string> options)
    {
        Command = command;
        Options = options;
    }

    public string? Command { get; }

    public IReadOnlyDictionary<string, string> Options { get; }

    public static ShadowCommandLine Parse(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var option = arg[2..];
            var equalsIndex = option.IndexOf('=');
            if (equalsIndex >= 0)
            {
                options[option[..equalsIndex]] = option[(equalsIndex + 1)..];
                continue;
            }

            if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[option] = args[++index];
            }
            else
            {
                options[option] = "true";
            }
        }

        options.TryGetValue(CommandOption, out var command);
        if (string.IsNullOrWhiteSpace(command))
        {
            options.TryGetValue(CommandAlias, out command);
        }

        return new ShadowCommandLine(command, options);
    }
}