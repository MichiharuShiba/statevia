using Microsoft.Extensions.Logging;

namespace Statevia.Core.Engine.Tests.TestSupport;

/// <summary>
/// テスト用 <see cref="ILoggerFactory"/>。任意カテゴリの <see cref="ILogger{T}"/> 解決を同一インスタンスに固定する。
/// </summary>
internal sealed class SingleCategoryLoggerFactory<T> : ILoggerFactory
{
    private readonly ILogger<T> _logger;

    /// <summary>指定ロガーを常に返すファクトリを構築する。</summary>
    /// <param name="logger">返却するロガー。</param>
    public SingleCategoryLoggerFactory(ILogger<T> logger) => _logger = logger;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => _logger;

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
