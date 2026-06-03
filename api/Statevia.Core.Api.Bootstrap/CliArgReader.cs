namespace Statevia.Core.Api.Bootstrap;

/// <summary>CLI 引数読み取り。</summary>
internal static class CliArgReader
{
    /// <summary>フラグの次トークンを必須値として読む。</summary>
    public static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {flag}.");
        index++;
        return args[index];
    }
}
