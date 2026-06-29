using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Api.Application.Actions.Modules;

namespace Statevia.Core.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="CompositeModuleSource"/> の集約・重複解決・tie-break の単体テスト。</summary>
public sealed class CompositeModuleSourceTests
{
    /// <summary>Priority 昇順で連結し、同一 Module の重複は高優先（小さい値）側が勝つ。DI 順に依存しない。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenDuplicateModule_KeepsHigherPrioritySource()
    {
        // Arrange
        var highPriority = new FakeModuleSource(
            priority: 10,
            new DiscoveredModule("alpha", "/high/alpha.dll", SourceLabel: "high"));
        var lowPriority = new FakeModuleSource(
            priority: 200,
            new DiscoveredModule("alpha", "/low/alpha.dll", SourceLabel: "low"),
            new DiscoveredModule("beta", "/low/beta.dll", SourceLabel: "low"));
        var sut = CreateSut(lowPriority, highPriority);

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, discovered.Count);
        var alpha = Assert.Single(discovered, module => module.ModuleDirectoryName == "alpha");
        Assert.Equal("/high/alpha.dll", alpha.EntryAssemblyPath);
        Assert.Equal("high", alpha.SourceLabel);
        Assert.Contains(discovered, module => module.ModuleDirectoryName == "beta");
    }

    /// <summary>同 Priority の重複は SourceLabel 昇順で tie-break する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenSamePriorityDuplicate_BreaksTieBySourceLabel()
    {
        // Arrange
        var sourceB = new FakeModuleSource(
            priority: 50,
            new DiscoveredModule("dup", "/b/dup.dll", SourceLabel: "bbb"));
        var sourceA = new FakeModuleSource(
            priority: 50,
            new DiscoveredModule("dup", "/a/dup.dll", SourceLabel: "aaa"));
        var sut = CreateSut(sourceB, sourceA);

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var winner = Assert.Single(discovered);
        Assert.Equal("aaa", winner.SourceLabel);
        Assert.Equal("/a/dup.dll", winner.EntryAssemblyPath);
    }

    /// <summary>大文字小文字を無視して同一 Module と判定し重複排除する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenDuplicateDiffersByCase_DeduplicatesCaseInsensitively()
    {
        // Arrange
        var highPriority = new FakeModuleSource(
            priority: 1,
            new DiscoveredModule("Sample", "/high/sample.dll", SourceLabel: "high"));
        var lowPriority = new FakeModuleSource(
            priority: 2,
            new DiscoveredModule("sample", "/low/sample.dll", SourceLabel: "low"));
        var sut = CreateSut(highPriority, lowPriority);

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var winner = Assert.Single(discovered);
        Assert.Equal("/high/sample.dll", winner.EntryAssemblyPath);
    }

    /// <summary>自身（CompositeModuleSource）が混入しても防御的に除外する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenNestedComposite_ExcludesItself()
    {
        // Arrange
        var inner = CreateSut(new FakeModuleSource(
            priority: 1,
            new DiscoveredModule("inner", "/inner/inner.dll", SourceLabel: "inner")));
        var direct = new FakeModuleSource(
            priority: 5,
            new DiscoveredModule("direct", "/direct/direct.dll", SourceLabel: "direct"));
        var sut = CreateSut(direct, inner);

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var single = Assert.Single(discovered);
        Assert.Equal("direct", single.ModuleDirectoryName);
    }

    /// <summary>Source が無ければ空を返す。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenNoSources_ReturnsEmpty()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>キャンセル要求時は集約を行わず例外を伝播する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenCancelled_Throws()
    {
        // Arrange
        var sut = CreateSut(new FakeModuleSource(
            priority: 1,
            new DiscoveredModule("alpha", "/alpha.dll", SourceLabel: "a")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act / Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.DiscoverAsync(cts.Token));
    }

    private static CompositeModuleSource CreateSut(params IModuleSource[] sources) =>
        new(sources, NullLogger<CompositeModuleSource>.Instance);

    private sealed class FakeModuleSource(int priority, params DiscoveredModule[] modules) : IModuleSource
    {
        public int Priority { get; } = priority;

        public Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<DiscoveredModule>>(modules);
        }
    }
}
