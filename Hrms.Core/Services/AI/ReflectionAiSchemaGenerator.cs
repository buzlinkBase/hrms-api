using Google.GenAI.Types;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace Hrms.Core.Services.AI;
/// <summary>
/// Builds a Gemini response schema from a CLR type using reflection.
/// Property names follow [JsonProperty] when present, otherwise camelCase.
/// [AiDescription] adds field guidance for the model; [AiIgnore] excludes a property.
/// Generated schemas are cached per type, so reflection runs once per T.
/// </summary>
public class ReflectionAiSchemaGenerator : IAiSchemaGenerator
{
    private static readonly ConcurrentDictionary<System.Type, Schema> SchemaCache = new();

    public Schema Generate(System.Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return SchemaCache.GetOrAdd(type, t => Build(t, new HashSet<System.Type>()));
    }

    private Schema Build(System.Type type, HashSet<System.Type> ancestors)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            var nullableSchema = Build(underlying, ancestors);
            nullableSchema.Nullable = true;
            return nullableSchema;
        }

        if (type == typeof(string))
            return new Schema { Type = Google.GenAI.Types.Type.String, Nullable = true };

        if (type == typeof(bool))
            return new Schema { Type = Google.GenAI.Types.Type.Boolean };

        if (type == typeof(int) || type == typeof(short) || type == typeof(byte))
            return new Schema { Type = Google.GenAI.Types.Type.Integer, Format = "int32" };

        if (type == typeof(long))
            return new Schema { Type = Google.GenAI.Types.Type.Integer, Format = "int64" };

        if (type == typeof(float))
            return new Schema { Type = Google.GenAI.Types.Type.Number, Format = "float" };

        if (type == typeof(double) || type == typeof(decimal))
            return new Schema { Type = Google.GenAI.Types.Type.Number, Format = "double" };

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return new Schema
            {
                Type = Google.GenAI.Types.Type.String,
                Description = "Date-time in ISO 8601 format (yyyy-MM-ddTHH:mm:ss)."
            };

        if (type == typeof(DateOnly))
            return new Schema
            {
                Type = Google.GenAI.Types.Type.String,
                Description = "Date in ISO 8601 format (yyyy-MM-dd)."
            };

        if (type == typeof(Guid))
            return new Schema { Type = Google.GenAI.Types.Type.String };

        if (type.IsEnum)
            return new Schema
            {
                Type = Google.GenAI.Types.Type.String,
                Enum = System.Enum.GetNames(type).ToList()
            };

        var elementType = GetEnumerableElementType(type);
        if (elementType != null)
            return new Schema
            {
                Type = Google.GenAI.Types.Type.Array,
                Items = Build(elementType, ancestors)
            };

        if (!type.IsClass || type == typeof(object))
            throw new NotSupportedException(
                $"Cannot build an AI response schema for '{type.FullName}'. " +
                "Use simple properties, enums, lists, or nested classes.");

        if (!ancestors.Add(type))
            throw new NotSupportedException(
                $"Circular reference detected while building the AI response schema for '{type.FullName}'.");

        try
        {
            var properties = new Dictionary<string, Schema>();
            var ordering = new List<string>();

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite)
                    continue;
                if (property.GetCustomAttribute<AiIgnoreAttribute>() != null)
                    continue;

                var propertySchema = Build(property.PropertyType, ancestors);

                var description = property.GetCustomAttribute<AiDescriptionAttribute>()?.Description;
                if (!string.IsNullOrWhiteSpace(description))
                    propertySchema.Description = description;

                var name = ResolvePropertyName(property);
                properties[name] = propertySchema;
                ordering.Add(name);
            }

            if (properties.Count == 0)
                throw new NotSupportedException(
                    $"'{type.FullName}' has no usable public properties for an AI response schema.");

            var objectSchema = new Schema
            {
                Type = Google.GenAI.Types.Type.Object,
                Properties = properties,
                Required = ordering.ToList(),
                PropertyOrdering = ordering
            };

            var classDescription = type.GetCustomAttribute<AiDescriptionAttribute>()?.Description;
            if (!string.IsNullOrWhiteSpace(classDescription))
                objectSchema.Description = classDescription;

            return objectSchema;
        }
        finally
        {
            ancestors.Remove(type);
        }
    }

    private static string ResolvePropertyName(PropertyInfo property)
    {
        var jsonProperty = property.GetCustomAttribute<JsonPropertyAttribute>();
        if (!string.IsNullOrWhiteSpace(jsonProperty?.PropertyName))
            return jsonProperty.PropertyName;

        var name = property.Name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static System.Type GetEnumerableElementType(System.Type type)
    {
        if (type == typeof(string))
            return null;

        if (type.IsArray)
            return type.GetElementType();

        if (!typeof(IEnumerable).IsAssignableFrom(type))
            return null;

        var enumerableInterface = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableInterface?.GetGenericArguments()[0];
    }
}
