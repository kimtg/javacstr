using System.Text;

namespace Javacstr.Core.Mapping;

public static class CasingHelper
{
    public static string ToPascalCase(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return identifier;
        if (char.IsUpper(identifier[0])) return identifier;

        var sb = new StringBuilder(identifier.Length);
        sb.Append(char.ToUpperInvariant(identifier[0]));
        if (identifier.Length > 1)
        {
            sb.Append(identifier.Substring(1));
        }
        return sb.ToString();
    }

    public static string ToCamelCase(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return identifier;
        if (char.IsLower(identifier[0])) return identifier;

        var sb = new StringBuilder(identifier.Length);
        sb.Append(char.ToLowerInvariant(identifier[0]));
        if (identifier.Length > 1)
        {
            sb.Append(identifier.Substring(1));
        }
        return sb.ToString();
    }
}
