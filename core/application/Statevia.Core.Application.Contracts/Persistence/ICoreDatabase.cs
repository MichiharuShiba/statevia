namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// EF Core DbContext 実装のマーカー。
/// Contracts は EF 型を参照せず、インフラ実装がこのインターフェースを実装する。
/// </summary>
public interface ICoreDatabase
{
}
