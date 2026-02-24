using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hrms.Api;

public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    private static readonly HashSet<string> Exceptions = new HashSet<string>
    {
        "SSS", "TIN", "HDMF","PHIC"
    };

    public override string ConvertName(string name)
    {
        // Replace known acronyms with lowercase versions
        foreach (var exception in Exceptions)
        {
            if (name.Contains(exception))
            {
                name = name.Replace(exception, exception.ToLower());
            }
        }

        // Convert PascalCase to snake_case
        var result = Regex.Replace(name, @"([a-z0-9])([A-Z])", "$1_$2");

        return result.ToLower();
    }
}