using System.Diagnostics.CodeAnalysis;

namespace Statevia.Core.Api.Application.Actions.Versioning;

/// <summary>compile 時の版解決に失敗したことを表す例外。</summary>
/// <remarks>
/// レンジを満たす版が見つからない、または exact 指定版が未ロードのときに投げる。Runtime は本例外経路に
/// 依存せず、解決はあくまで compile 時に完結する。
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "例外型は呼び出し側が捕捉できるよう公開する（S3871）。")]
public sealed class ModuleVersionResolutionException : Exception
{
    /// <summary>既定メッセージで生成する。</summary>
    public ModuleVersionResolutionException()
    {
    }

    /// <summary>メッセージを指定して生成する。</summary>
    /// <param name="message">失敗理由。</param>
    public ModuleVersionResolutionException(string message)
        : base(message)
    {
    }

    /// <summary>メッセージと内部例外を指定して生成する。</summary>
    /// <param name="message">失敗理由。</param>
    /// <param name="innerException">内部例外。</param>
    public ModuleVersionResolutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
