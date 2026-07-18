using System;
using System.Collections.Generic;

namespace Shadow.Plugins;

internal sealed class ShadowCommandLine
{
    private const string CommandOption = "shadow-command";
    private const string CommandAlias = "command";

    private ShadowCommandLine(
        string? command,
        IReadOnlyDictionary<string, string> options,
        IReadOnlyList<string> positionals)
    {
        Command = command;
        Options = options;
        Positionals = positionals;
    }

    public string? Command { get; }

    public IReadOnlyDictionary<string, string> Options { get; }

    public IReadOnlyList<string> Positionals { get; }

    public static ShadowCommandLine Parse(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positionals = new List<string>();
        string? command = null;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (IsOptionToken(arg))
            {
                var optionName = NormalizeOptionName(arg);
                var equalsIndex = optionName.IndexOf('=');
                if (equalsIndex >= 0)
                {
                    SetOption(options, optionName[..equalsIndex], optionName[(equalsIndex + 1)..]);
                    continue;
                }

                if (IsFlagOption(optionName))
                {
                    SetOption(options, optionName, "true");
                    continue;
                }

                if (index + 1 < args.Count && !IsOptionToken(args[index + 1]))
                {
                    SetOption(options, optionName, args[++index]);
                }
                else
                {
                    SetOption(options, optionName, "true");
                }

                continue;
            }

            if (command is null)
            {
                command = NormalizeCommandName(arg);
                continue;
            }

            positionals.Add(arg);
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            options.TryGetValue(CommandOption, out command);
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            options.TryGetValue(CommandAlias, out command);
        }

        if (!string.IsNullOrWhiteSpace(command))
        {
            command = NormalizeCommandName(command);
        }

        ApplyPositionalDefaults(command, positionals, options);
        return new ShadowCommandLine(command, options, positionals);
    }

    private static void ApplyPositionalDefaults(
        string? command,
        IReadOnlyList<string> positionals,
        IDictionary<string, string> options)
    {
        if (string.IsNullOrWhiteSpace(command) || positionals.Count == 0)
        {
            return;
        }

        if (string.Equals(command, "paradox.launch", StringComparison.OrdinalIgnoreCase))
        {
            if (!HasAnyOption(options, "game", "game-id", "gameId") && positionals.Count >= 1)
            {
                options["game"] = positionals[0];
            }

            if (!HasAnyOption(options, "playset", "playset-id", "playsetId") && positionals.Count >= 2)
            {
                options["playset"] = positionals[1];
            }
        }
    }

    private static bool HasAnyOption(IDictionary<string, string> options, params string[] names)
    {
        foreach (var name in names)
        {
            if (options.ContainsKey(name))
            {
                return true;
            }
        }

        return false;
    }

    private static void SetOption(IDictionary<string, string> options, string name, string value)
    {
        var normalized = NormalizeOptionName(name);
        options[normalized] = value;

        switch (normalized)
        {
            case "p":
            case "playset":
            case "playset-id":
            case "playsetid":
                options["playset"] = value;
                options["playset-id"] = value;
                options["playsetId"] = value;
                break;
            case "g":
            case "game":
            case "game-id":
            case "gameid":
                options["game"] = value;
                options["game-id"] = value;
                options["gameId"] = value;
                break;
            case "debug":
                options["debug"] = value;
                break;
            case "allow-missing-mods":
            case "allowmissingmods":
                options["allow-missing-mods"] = value;
                break;
            case "shadow-command":
            case "command":
                options[CommandOption] = value;
                options[CommandAlias] = value;
                break;
        }
    }

    private static string NormalizeCommandName(string value)
    {
        var command = value.Trim();
        return command.ToLowerInvariant() switch
        {
            "pdxgamelauncher" => "paradox.launch",
            "paradoxgamelauncher" => "paradox.launch",
            "paradox.launch" => "paradox.launch",
            "paradox-launch" => "paradox.launch",
            "launch" => "paradox.launch",
            "hoi4.launch" => "paradox.launch",
            _ => command,
        };
    }

    private static string NormalizeOptionName(string value)
    {
        var option = value.Trim();
        while (option.StartsWith('-'))
        {
            option = option[1..];
        }

        return option;
    }

    private static bool IsOptionToken(string value)
    {
        return value.StartsWith('-') && value.Length > 1 && !string.Equals(value, "-", StringComparison.Ordinal);
    }

    private static bool IsFlagOption(string optionName)
    {
        return optionName.Equals("debug", StringComparison.OrdinalIgnoreCase)
               || optionName.Equals("allow-missing-mods", StringComparison.OrdinalIgnoreCase)
               || optionName.Equals("allowmissingmods", StringComparison.OrdinalIgnoreCase);
    }
}

