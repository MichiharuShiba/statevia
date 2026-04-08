using Statevia.Core.Engine.Infrastructure.Logging;

namespace Statevia.Core.Api.Hosting;

/// <summary>
/// ログ用スナップショットの簡易マスキング（STV-408 前の妥協実装）。
/// </summary>
public static class LogBodyRedactor
{
    /// <summary>
    /// 共通 `LogRedaction` へ委譲してマスク済み文字列を返す。
    /// </summary>
    public static string Redact(string? text, int maxChars) => LogRedaction.Redact(text, maxChars);
}
