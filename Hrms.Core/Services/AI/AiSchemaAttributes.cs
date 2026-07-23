namespace Hrms.Core.Services.AI;

/// <summary>
/// Describes a property to the model so it knows what value to extract.
/// The description becomes part of the response schema sent to Gemini.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AiDescriptionAttribute : Attribute
{
    public string Description { get; }

    public AiDescriptionAttribute(string description)
    {
        Description = description;
    }
}

/// <summary>
/// Excludes a property from the response schema; the model will never fill it.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class AiIgnoreAttribute : Attribute
{
}
