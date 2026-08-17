using System.Text;

namespace Statevia.Service.Cli.Infrastructure;

/// <summary>CLI の対話入力。機密はコンソールでマスクする。</summary>
/// <remarks>
/// <para>空入力は「現在値を維持」と解釈する。呼び出し側が現在値を渡す。</para>
/// <para>TTY では 1 文字ずつ <c>*</c> を出す。標準入力のリダイレクト、およびテストの <see cref="StringReader"/> 差し替え時は <see cref="Console.ReadLine"/> 相当（OS のリダイレクト判定だけでは <c>Console.SetIn</c> を検知できない）。</para>
/// <para>機密の実値はプロンプトへ出さない。設定済みなら <c>********</c> のみ示す。</para>
/// </remarks>
internal static class CliConsolePrompt
{
    internal const string SecretMask = "********";

    /// <summary>1 項目を尋ねる。空入力なら <paramref name="current"/> を返す。</summary>
    /// <param name="label">項目名（例: <c>api-base</c>）。</param>
    /// <param name="current">現在値。機密でも実値を渡してよい（表示には使わない）。</param>
    /// <param name="secret">true のとき入力をマスクし、初期値表示もマスクする。</param>
    /// <returns>採用する値。未設定のままなら <see langword="null"/>。</returns>
    public static async Task<string?> ReadAsync(string label, string? current, bool secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var hint = FormatHint(current, secret);
        var suffix = hint is null ? ": " : $" [{hint}]: ";
        await Console.Out.WriteAsync(label + suffix).ConfigureAwait(false);

        var entered = secret
            ? ReadSecretLine()
            : await Console.In.ReadLineAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(entered))
            return NullIfEmpty(current);

        return entered.Trim();
    }

    /// <summary>パスワードなど、初期値なしの機密入力。</summary>
    /// <param name="label">プロンプト（例: <c>Password</c>）。</param>
    /// <returns>入力。空なら <see langword="null"/>。</returns>
    public static async Task<string?> ReadSecretAsync(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        await Console.Out.WriteAsync($"{label}: ").ConfigureAwait(false);
        var entered = ReadSecretLine();
        return string.IsNullOrWhiteSpace(entered) ? null : entered;
    }

    private static string? ReadSecretLine()
    {
        if (Console.IsInputRedirected || Console.In is StringReader)
            return Console.In.ReadLine();

        return ReadMaskedLine(() => Console.ReadKey(intercept: true));
    }

    /// <summary>TTY 相当のマスク入力。テストからキー列を注入する。</summary>
    /// <param name="readKey">次のキーを返す。</param>
    /// <returns>確定した行（Enter まで。末尾改行なし）。</returns>
    internal static string ReadMaskedLine(Func<ConsoleKeyInfo> readKey)
    {
        ArgumentNullException.ThrowIfNull(readKey);
        var builder = new StringBuilder();
        while (true)
        {
            var key = readKey();
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return builder.ToString();
                case ConsoleKey.Backspace:
                    if (builder.Length == 0)
                        continue;
                    builder.Length--;
                    Console.Write("\b \b");
                    continue;
                default:
                    if (key.KeyChar is '\0' || char.IsControl(key.KeyChar))
                        continue;
                    builder.Append(key.KeyChar);
                    Console.Write('*');
                    break;
            }
        }
    }

    /// <summary>プロンプトに出す初期値。機密はマスクし、未設定なら出さない。</summary>
    private static string? FormatHint(string? current, bool secret)
    {
        if (secret)
            return string.IsNullOrWhiteSpace(current) ? null : SecretMask;

        return NullIfEmpty(current);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
