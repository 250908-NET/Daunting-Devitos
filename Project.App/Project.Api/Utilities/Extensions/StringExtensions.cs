using System.Text;

namespace Project.Api.Utilities.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Converts a PascalCase string to snake_case. Designed to convert C# identifier names, and assumes ASCII-only.
    ///
    /// Adapted and simplified from part of the EF Core source code.
    /// see: https://github.com/efcore/EFCore.NamingConventions/blob/main/EFCore.NamingConventions/Internal/SnakeCaseNameRewriter.cs
    /// </summary>
    public static string ToSnakeCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // pre-allocate with a reasonable guess for capacity
        var builder = new StringBuilder(input.Length + Math.Min(2, input.Length / 5));

        for (var i = 0; i < input.Length; i++)
        {
            var currentChar = input[i];

            // add an underscore if the current character is uppercase and it's not the first character
            if (i > 0 && char.IsUpper(currentChar))
            {
                var prevChar = input[i - 1];

                // handles two cases:
                // 1. a transition from a lowercase or digit to an uppercase (e.g., "ValueUp" or "Value1Up").
                // 2. a transition from an acronym to a new word (e.g., the "E" in "SSEEvent").
                if (!char.IsUpper(prevChar) || (i + 1 < input.Length && char.IsLower(input[i + 1])))
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToLowerInvariant(currentChar));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Converts a snake_case string to PascalCase.
    /// </summary>
    public static string ToPascalCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return string.Concat(input.Split('_').Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    }
}
