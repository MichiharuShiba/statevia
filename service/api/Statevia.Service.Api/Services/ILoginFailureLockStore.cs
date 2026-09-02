namespace Statevia.Service.Api.Services;

/// <summary>ログイン失敗回数に基づくロックの読み書き。</summary>
/// <remarks>
/// キーはテナントキー＋ユーザー名。呼び出し側は実在するユーザーのパスワード不正だけを記録する。
/// </remarks>
internal interface ILoginFailureLockStore
{
    /// <summary>指定主体がロック中か。</summary>
    /// <param name="tenantKey">テナントキー。</param>
    /// <param name="username">ユーザー名。</param>
    /// <returns>ロック中なら true。</returns>
    bool IsLocked(string tenantKey, string username);

    /// <summary>失敗を 1 回記録し、閾値に達したらロックする。</summary>
    /// <param name="tenantKey">テナントキー。</param>
    /// <param name="username">ユーザー名。</param>
    void RecordFailure(string tenantKey, string username);

    /// <summary>成功ログイン後に失敗カウントとロックを消す。</summary>
    /// <param name="tenantKey">テナントキー。</param>
    /// <param name="username">ユーザー名。</param>
    void Reset(string tenantKey, string username);
}
