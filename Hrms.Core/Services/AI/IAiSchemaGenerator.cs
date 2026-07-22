using Google.GenAI.Types;

namespace Hrms.Core.Services.AI;

/// <summary>
/// Strategy for turning a CLR type into a Gemini response schema.
/// Swap the implementation to change how schemas are produced
/// (e.g. attribute-driven reflection today, JSON-schema files tomorrow).
/// </summary>
public interface IAiSchemaGenerator
{
    Schema Generate(System.Type type);
}
