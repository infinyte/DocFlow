using System.Text;

namespace DocFlow.Documentation.Markdown;

/// <summary>
/// Deterministic slug utilities for filenames and cross-links.
/// </summary>
internal static class Slug
{
    /// <summary>
    /// Convert an arbitrary display string (tag name, entity name, path segment) into a
    /// lowercase kebab-case slug suitable for filenames and anchor ids.
    /// </summary>
    public static string Kebab(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        var previousWasDash = false;

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            if (char.IsLetterOrDigit(ch))
            {
                // Insert a dash before a capital that follows a lowercase or digit (camelCase → kebab-case).
                if (char.IsUpper(ch) && i > 0 && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1])) && !previousWasDash)
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(ch));
                previousWasDash = false;
            }
            else if (!previousWasDash && builder.Length > 0)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
