using Statevia.Core.Engine.Definition;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary><see cref="ModuleImportReference"/> の import 解析テスト。</summary>
public sealed class ModuleImportReferenceTests
{
    /// <summary>@ 省略の import は ModuleId のみ・空レンジとして解析する。</summary>
    [Fact]
    public void ParseImportValue_WhenWithoutRange_ParsesModuleIdOnly()
    {
        // Act
        var import = ModuleImportReference.ParseImportValue("com.company.mail");

        // Assert
        Assert.Equal("com.company.mail", import.ModuleId);
        Assert.Equal(string.Empty, import.VersionRange);
    }

    /// <summary>moduleId@range 形式を構造化する。</summary>
    [Fact]
    public void ParseImportValue_WhenWithRange_SplitsModuleIdAndRange()
    {
        // Act
        var import = ModuleImportReference.ParseImportValue("demo.module@^1.2");

        // Assert
        Assert.Equal("demo.module", import.ModuleId);
        Assert.Equal("^1.2", import.VersionRange);
    }

    /// <summary>@ のみの import は構文エラーになる。</summary>
    [Fact]
    public void ParseImportValue_WhenAtSignOnly_Throws()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ModuleImportReference.ParseImportValue("@^1.0"));

        Assert.Contains("non-empty ModuleId", ex.Message, StringComparison.Ordinal);
    }
}
