using System.Text.Json;

namespace Statevia.Core.Actions.Abstractions.Publication;

/// <summary>AI / Doc 向け action 入力例。</summary>
/// <param name="Title">例の表示名。</param>
/// <param name="Input">例示する input マップ（JSON）。</param>
public sealed record ActionExample(string Title, JsonDocument Input);
