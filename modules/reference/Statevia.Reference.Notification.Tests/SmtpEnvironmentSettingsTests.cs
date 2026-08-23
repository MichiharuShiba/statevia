using System.Net.Mail;
using Statevia.Core.Engine.Abstractions;
using Statevia.Reference.Notification;

namespace Statevia.Reference.Notification.Tests;

/// <summary>SMTP 環境変数の読取を他テストと直列化する。</summary>
[CollectionDefinition("SmtpEnvironment", DisableParallelization = true)]
public sealed class SmtpEnvironmentCollectionDefinition;

/// <summary><see cref="SmtpEnvironmentSettings"/> の環境変数解決。</summary>
[Collection("SmtpEnvironment")]
public sealed class SmtpEnvironmentSettingsTests
{
    /// <summary>HOST が無いとき失敗する。</summary>
    [Fact]
    public void Read_MissingHost_Throws()
    {
        SmtpEnvironmentLock.Run(() =>
        {
            // Arrange
            SmtpEnvironmentLock.Clear();

            // Act / Assert
            var ex = Assert.Throws<InvalidOperationException>(SmtpEnvironmentSettings.Read);
            Assert.Contains("STATEVIA_SMTP_HOST", ex.Message, StringComparison.Ordinal);
        });
    }

    /// <summary>PORT 未設定なら 587 を使う。</summary>
    [Fact]
    public void Read_WithoutPort_UsesDefault587()
    {
        SmtpEnvironmentLock.Run(() =>
        {
            // Arrange
            SmtpEnvironmentLock.Clear();
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_HOST", " smtp.example.com ");

            // Act
            var settings = SmtpEnvironmentSettings.Read();

            // Assert
            Assert.Equal("smtp.example.com", settings.Host);
            Assert.Equal(587, settings.Port);
            Assert.Null(settings.User);
            Assert.Null(settings.Password);
            Assert.Null(settings.DefaultFrom);
        });
    }

    /// <summary>PORT / USER / PASSWORD / FROM を読む。</summary>
    [Fact]
    public void Read_WithAllVariables_ReturnsTrimmedValues()
    {
        SmtpEnvironmentLock.Run(() =>
        {
            // Arrange
            SmtpEnvironmentLock.Clear();
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_HOST", "smtp.example.com");
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_PORT", "2525");
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_USER", "  user  ");
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_PASSWORD", "  secret  ");
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_FROM", "  noreply@example.com  ");

            // Act
            var settings = SmtpEnvironmentSettings.Read();

            // Assert
            Assert.Equal(2525, settings.Port);
            Assert.Equal("user", settings.User);
            Assert.Equal("secret", settings.Password);
            Assert.Equal("noreply@example.com", settings.DefaultFrom);
        });
    }

    /// <summary>PORT が数値でないとき既定 587 を使う。</summary>
    [Fact]
    public void Read_NonNumericPort_UsesDefault587()
    {
        SmtpEnvironmentLock.Run(() =>
        {
            // Arrange
            SmtpEnvironmentLock.Clear();
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_HOST", "smtp.example.com");
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_PORT", "abc");

            // Act
            var settings = SmtpEnvironmentSettings.Read();

            // Assert
            Assert.Equal(587, settings.Port);
        });
    }
}

/// <summary>from 未指定時の環境変数フォールバック。</summary>
[Collection("SmtpEnvironment")]
public sealed class NotificationSendFromAddressTests
{
    /// <summary>input.from が空なら STATEVIA_SMTP_FROM を使う。</summary>
    [Fact]
    public Task ExecuteAsync_MissingFrom_UsesEnvironmentDefaultFrom() =>
        SmtpEnvironmentLock.RunAsync(async () =>
        {
            // Arrange
            SmtpEnvironmentLock.Clear();
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_HOST", "smtp.example.com");
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_FROM", "env@example.com");
            MailMessage? captured = null;
            var state = new NotificationSendActionState((message, _) =>
            {
                captured = message;
                return Task.CompletedTask;
            });
            var input = new Dictionary<string, object?>
            {
                ["channel"] = "email",
                ["to"] = "user@example.com",
                ["subject"] = "hello",
                ["body"] = "world",
            };

            // Act
            await state.ExecuteAsync(CreateContext(), input, CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Equal("env@example.com", captured.From?.Address);
        });

    /// <summary>from も環境変数も無いとき失敗する。</summary>
    [Fact]
    public Task ExecuteAsync_MissingFromAndEnvironment_Throws() =>
        SmtpEnvironmentLock.RunAsync(async () =>
        {
            // Arrange
            SmtpEnvironmentLock.Clear();
            Environment.SetEnvironmentVariable("STATEVIA_SMTP_HOST", "smtp.example.com");
            var state = new NotificationSendActionState((_, _) => Task.CompletedTask);
            var input = new Dictionary<string, object?>
            {
                ["channel"] = "email",
                ["to"] = "user@example.com",
                ["subject"] = "hello",
                ["body"] = "world",
            };

            // Act / Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                state.ExecuteAsync(CreateContext(), input, CancellationToken.None));
            Assert.Contains("STATEVIA_SMTP_FROM", ex.Message, StringComparison.Ordinal);
        });

    private static StateContext CreateContext() =>
        new()
        {
            Events = new StubEventProvider(),
            Store = new StubStateStore(),
            ExecutionId = Guid.NewGuid().ToString("D"),
            StateName = "Notify",
        };

    private sealed class StubEventProvider : IEventProvider
    {
        public Task WaitAsync(string eventName, CancellationToken ct) => Task.CompletedTask;

        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct) =>
            Task.FromResult(eventNames[0]);

        public void Signal(string signalName)
        {
        }

        public void Resume(string nodeId, string eventName)
        {
        }

        public void PublishTopic(string topic, object? payloadSummary)
        {
        }
    }

    private sealed class StubStateStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output)
        {
            output = null;
            return false;
        }
    }
}

/// <summary>SMTP 環境変数の更新を直列化する。</summary>
internal static class SmtpEnvironmentLock
{
    private static readonly object Gate = new();

    private static readonly string[] VariableNames =
    [
        "STATEVIA_SMTP_HOST",
        "STATEVIA_SMTP_PORT",
        "STATEVIA_SMTP_USER",
        "STATEVIA_SMTP_PASSWORD",
        "STATEVIA_SMTP_FROM",
    ];

    /// <summary>環境変数を空にする。</summary>
    public static void Clear()
    {
        foreach (var name in VariableNames)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    /// <summary>ロック下で同期テストを実行し、終了時に変数を戻す。</summary>
    public static void Run(Action body)
    {
        lock (Gate)
        {
            var previous = Capture();
            try
            {
                body();
            }
            finally
            {
                Restore(previous);
            }
        }
    }

    /// <summary>環境変数を退避して非同期テストを実行し、終了時に戻す。</summary>
    public static async Task RunAsync(Func<Task> body)
    {
        var previous = Capture();
        try
        {
            await body().ConfigureAwait(false);
        }
        finally
        {
            Restore(previous);
        }
    }

    private static Dictionary<string, string?> Capture() =>
        VariableNames.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);

    private static void Restore(IReadOnlyDictionary<string, string?> previous)
    {
        foreach (var pair in previous)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}
