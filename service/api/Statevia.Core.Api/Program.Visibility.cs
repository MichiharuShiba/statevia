namespace Statevia.Core.Api;

/// <summary>
/// トップレベル <c>Program</c> を統合テスト（WebApplicationFactory）から参照可能にする。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Avoid uninstantiated internal classes", Justification = "WebApplicationFactory のエントリポイント公開に必要。")]
public partial class Program;
