using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.ActionHost.Modules;

/// <summary>Action Host に load 済みの 1 Action。</summary>
/// <param name="ActionId">canonical actionId。</param>
/// <param name="Executor">実行器。</param>
/// <param name="ModuleId">所属 Module ID。</param>
internal sealed record LoadedActionRegistration(string ActionId, IStateExecutor Executor, string ModuleId);

/// <summary>load 済み Action の actionId 解決レジストリ。</summary>
internal sealed class ActionHostActionRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, LoadedActionRegistration> _actions = new(StringComparer.Ordinal);

    /// <summary>登録済み Action 数。</summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _actions.Count;
            }
        }
    }

    /// <summary>Action を登録する。同一 actionId は後勝ちしない（スキップ）。</summary>
    /// <param name="registration">登録情報。</param>
    /// <returns>登録できた場合は true。</returns>
    public bool TryRegister(LoadedActionRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.ActionId);

        lock (_sync)
        {
            if (_actions.ContainsKey(registration.ActionId))
            {
                return false;
            }

            _actions[registration.ActionId] = registration;
            return true;
        }
    }

    /// <summary>actionId から登録を取得する。</summary>
    /// <param name="actionId">canonical actionId。</param>
    /// <param name="registration">登録情報。</param>
    /// <returns>見つかった場合は true。</returns>
    public bool TryGet(string actionId, out LoadedActionRegistration? registration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        lock (_sync)
        {
            if (_actions.TryGetValue(actionId, out var found))
            {
                registration = found;
                return true;
            }
        }

        registration = null;
        return false;
    }
}
