namespace Statevia.Core.Application.Contracts.Security;

/// <summary><see cref="PrincipalType"/> の監査表示ラベル。</summary>
public static class PrincipalTypeLabels
{
    /// <summary>Principal 種別をスナップショット用ラベルへ変換する。</summary>
    /// <param name="principalType">Principal 種別。</param>
    public static string ToSnapshotLabel(PrincipalType principalType) =>
        principalType switch
        {
            PrincipalType.User => "User",
            PrincipalType.ServiceAccount => "ServiceAccount",
            PrincipalType.System => "System",
            _ => principalType.ToString()
        };
}
