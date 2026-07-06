using System.Diagnostics.CodeAnalysis;

namespace Statevia.Service.Api.Application.Actions.Versioning;

/// <summary>版共存移行（再 compile または版配置）が必要な定義状態を表す例外。</summary>
/// <remarks>
/// <para>HTTP API では <c>DEFINITION_MIGRATION_REQUIRED</c>（422）として返す。</para>
/// <para>
/// 例: Legacy compiled JSON に bindings が無い状態で複数版がロードされている、ピン版が未ロード、
/// FQCN 参照で複数版がロードされている。
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "例外型は呼び出し側が捕捉できるよう公開する（S3871）。")]
public sealed class DefinitionMigrationRequiredException : Exception
{
    /// <summary>API エラーコード。</summary>
    public const string ErrorCode = "DEFINITION_MIGRATION_REQUIRED";

    /// <summary>既定メッセージで生成する。</summary>
    public DefinitionMigrationRequiredException()
    {
    }

    /// <summary>メッセージを指定して生成する。</summary>
    /// <param name="message">移行が必要な理由。</param>
    public DefinitionMigrationRequiredException(string message)
        : base(message)
    {
    }

    /// <summary>メッセージと内部例外を指定して生成する。</summary>
    /// <param name="message">移行が必要な理由。</param>
    /// <param name="innerException">内部例外。</param>
    public DefinitionMigrationRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
