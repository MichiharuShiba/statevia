namespace Statevia.Service.Cli.Infrastructure;

/// <summary>ホーム配下の設定／秘密ファイルが不正なときに投げる。</summary>
/// <remarks>メッセージにファイル中身は含めない。</remarks>
public sealed class CliHomeFileException : Exception
{
    /// <summary>不正だったファイルのパス。</summary>
    public string FilePath { get; }

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="filePath">対象ファイル。</param>
    /// <param name="reason">利用者向けの短い理由（中身は含めない）。</param>
    public CliHomeFileException(string filePath, string reason)
        : base($"Invalid file: {filePath} ({reason}).")
    {
        FilePath = filePath;
    }
}
