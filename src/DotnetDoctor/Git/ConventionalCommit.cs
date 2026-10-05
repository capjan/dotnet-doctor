namespace DotnetDoctor.Git;

internal static class ConventionalCommit
{
    public const string HookName = "commit-msg";

    public const string HookScript = """
        #!/bin/sh

        header=$(sed -n '1p' "$1")
        if printf '%s\n' "$header" | grep -Eq '^[[:lower:]][[:lower:][:digit:]-]*(\([^()]+\))?!?: [^[:space:]].*$'; then
            exit 0
        fi

        printf '%s\n' 'Commit type must be lowercase and follow Conventional Commits: <type>[optional scope][!]: <description>' >&2
        exit 1
        """;

    public static bool IsValidMessage(string message)
    {
        var header = message.Split(new[] { '\r', '\n' }, 2)[0];
        var separator = header.IndexOf(": ", StringComparison.Ordinal);
        if (separator <= 0 || string.IsNullOrWhiteSpace(header[(separator + 2)..]))
        {
            return false;
        }

        var prefix = header[..separator];
        if (prefix.EndsWith('!'))
        {
            prefix = prefix[..^1];
        }

        var scopeStart = prefix.IndexOf('(');
        var type = scopeStart < 0 ? prefix : prefix[..scopeStart];
        if (type.Length == 0 || type.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
        {
            return false;
        }

        if (!string.Equals(type, type.ToLowerInvariant(), StringComparison.Ordinal))
        {
            return false;
        }

        return scopeStart < 0 ||
            (prefix.EndsWith(')') &&
             prefix.IndexOf(')', scopeStart) == prefix.Length - 1 &&
             !string.IsNullOrWhiteSpace(prefix[(scopeStart + 1)..^1]));
    }
}
