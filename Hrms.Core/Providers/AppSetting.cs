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
    private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
    {
        WriteIndented = true, // pretty-print JSON
        PropertyNameCaseInsensitive = true
    };

    // Serialize object to JSON string
    public static string Serialize<T>(T obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        return JsonSerializer.Serialize(obj, _options);
    }

    // Deserialize JSON string to object
    public static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON string is null or empty", nameof(json));

        return JsonSerializer.Deserialize<T>(json, _options)!;
    }
    // Safe deserialize with default fallback
    public static T DeserializeSafe<T>(string json, T defaultValue = default!)
    {
        try
        {
            return Deserialize<T>(json);
        }
        catch
        {
            return defaultValue;
        }
    }
}

