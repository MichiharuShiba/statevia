using Statevia.Service.Cli.Infrastructure;
using Xunit;

namespace Statevia.Service.Cli.Tests;

/// <summary>対話入力と TTY マスクのテスト。</summary>
public sealed class CliConsolePromptTests
{
    /// <summary>マスク入力は印字文字を保持し、制御キーを無視する。</summary>
    [Fact]
    public void ReadMaskedLine_TypesCharacters_IgnoresControlKeys()
    {
        // Arrange
        var keys = new Queue<ConsoleKeyInfo>([
            new('s', ConsoleKey.S, false, false, false),
            new('\0', ConsoleKey.LeftArrow, false, false, false),
            new('\u0001', ConsoleKey.A, false, false, false),
            new('e', ConsoleKey.E, false, false, false),
            new('\r', ConsoleKey.Enter, false, false, false),
        ]);
        var previousOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var result = CliConsolePrompt.ReadMaskedLine(keys.Dequeue);

            // Assert
            Assert.Equal("se", result);
            Assert.Contains('*', outWriter.ToString());
            Assert.DoesNotContain("se", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }

    /// <summary>Backspace は直前文字を消し、空のときは何もしない。</summary>
    [Fact]
    public void ReadMaskedLine_Backspace_RemovesLastCharacter()
    {
        // Arrange
        var keys = new Queue<ConsoleKeyInfo>([
            new('\b', ConsoleKey.Backspace, false, false, false),
            new('a', ConsoleKey.A, false, false, false),
            new('b', ConsoleKey.B, false, false, false),
            new('\b', ConsoleKey.Backspace, false, false, false),
            new('c', ConsoleKey.C, false, false, false),
            new('\r', ConsoleKey.Enter, false, false, false),
        ]);
        var previousOut = Console.Out;
        Console.SetOut(TextWriter.Null);

        try
        {
            // Act
            var result = CliConsolePrompt.ReadMaskedLine(keys.Dequeue);

            // Assert
            Assert.Equal("ac", result);
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }

    /// <summary>機密プロンプトは現在値をマスクして示し、空入力なら維持する。</summary>
    [Fact]
    public async Task ReadAsync_Secret_ShowsMaskAndKeepsCurrentOnEmpty()
    {
        // Arrange
        const string current = "real-secret-value";
        var previousIn = Console.In;
        var previousOut = Console.Out;
        Console.SetIn(new StringReader("\n"));
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var result = await CliConsolePrompt.ReadAsync("api-key", current, secret: true);

            // Assert
            Assert.Equal(current, result);
            Assert.Contains(CliConsolePrompt.SecretMask, outWriter.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(current, outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetIn(previousIn);
            Console.SetOut(previousOut);
        }
    }

    /// <summary>非秘密プロンプトは現在値を初期値として示す。</summary>
    [Fact]
    public async Task ReadAsync_NonSecret_ShowsCurrentValue()
    {
        // Arrange
        var previousIn = Console.In;
        var previousOut = Console.Out;
        Console.SetIn(new StringReader("new-tenant\n"));
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var result = await CliConsolePrompt.ReadAsync("tenant", "old-tenant", secret: false);

            // Assert
            Assert.Equal("new-tenant", result);
            Assert.Contains("old-tenant", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetIn(previousIn);
            Console.SetOut(previousOut);
        }
    }
}
