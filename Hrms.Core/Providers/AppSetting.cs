using System.Linq.Expressions;
using System.Text.Json;

namespace Hrms.Core.Providers;

public class AppSettingRepository
{
    private readonly List<AppGlobalSettingModel> _settings;
    public AppSettingRepository(List<AppGlobalSettingModel> settings)
    {
        _settings = settings;
    }
    public AppGlobalSettingModel? FindOne(string key)
    {
        return _settings.FirstOrDefault(x => x.SettingKey.Contains(key, StringComparison.OrdinalIgnoreCase));
    }
    public IEnumerable<AppGlobalSettingModel> Find(Expression<Func<AppGlobalSettingModel, bool>>? expression = null)
    {
        var predicate = (expression ?? (x => true)).Compile();
        return _settings.Where(predicate);
    }
}
public class AppSettingValueResolver
{
    public bool ResolveBool(string input, bool defaultValue = false)
    {
        if (bool.TryParse(input, out bool result))
        {
            return result;
        }
        return defaultValue;
    }

    public int ResolveInt(string input, int defaultValue = 0)
    {
        if (int.TryParse(input, out int result))
        {
            return result;
        }
        return defaultValue;
    }

    public double ResolveDouble(string input, double defaultValue = 0.0)
    {
        if (double.TryParse(input, out double result))
        {
            return result;
        }
        return defaultValue;
    }

    public DateTime ResolveDateTime(string input, DateTime? defaultValue = null)
    {
        if (DateTime.TryParse(input, out DateTime result))
        {
            return result;
        }
        return defaultValue ?? DateTime.MinValue;
    }

    public Guid ResolveGuid(string input, Guid? defaultValue = null)
    {
        if (Guid.TryParse(input, out Guid result))
        {
            return result;
        }
        return defaultValue ?? Guid.Empty;
    }

    public TEnum ResolveEnum<TEnum>(string input, TEnum defaultValue) where TEnum : struct
    {
        if (Enum.TryParse(input, true, out TEnum result))
        {
            return result;
        }
        return defaultValue;
    }

    public T ResolveObject<T>(string input, T defaultValue = default!)
    {
        if (string.IsNullOrWhiteSpace(input) )
            return defaultValue;
        try
        {
            return ObjectSerializer.Deserialize<T>(input); 
        }
        catch
        {
            return defaultValue;
        }
    }


    // 🗂️ Metadata resolver
    public Dictionary<string, string> ResolveMetaData(string? metaData)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(metaData))
            return dict;

        try
        {
            // Try JSON first
            dict = JsonSerializer.Deserialize<Dictionary<string, string>>(metaData)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            // Fallback: parse "key=value;key2=value2"
            var pairs = metaData.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2)
                {
                    dict[kv[0].Trim()] = kv[1].Trim();
                }
            }
        }
        return dict;
    }

}

public static class ObjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        // Good for web APIs or JS compatibility
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Serialize object to string
    public static string Serialize(object obj) =>
        JsonSerializer.Serialize(obj, Options);

    // Deserialize string to object with null check
    public static T? Deserialize<T>(string message) where T : class =>
        string.IsNullOrWhiteSpace(message)
            ? null
            : JsonSerializer.Deserialize<T>(message, Options);
}
