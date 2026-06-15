using System.Text.Json;

namespace Statevia.Actions.Abstractions.Publication;

/// <summary>型・制約（Language Neutral JSON Schema）。</summary>
/// <param name="InputSchema">action input の JSON Schema。</param>
/// <param name="OutputSchema">action output の JSON Schema。</param>
/// <param name="SchemaVersion">Schema 方言（既定: Draft 2020-12）。</param>
public sealed record ActionSchemaBundle(
    JsonDocument InputSchema,
    JsonDocument OutputSchema,
    string SchemaVersion = StateviaActionSchemaVocabulary.DefaultSchemaVersion);
