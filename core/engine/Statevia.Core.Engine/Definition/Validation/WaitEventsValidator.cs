using Statevia.Core.Engine.FSM;

namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// Wait State の <c>events</c>（イベント名 → 遷移先）を Level1 で検証する。
/// </summary>
/// <remarks>
/// <para>同一 Wait 内のイベント名重複・予約語・遷移先存在・自己遷移を検査する。</para>
/// <para>同一 execution 内のイベント名重複（Fork 並列 Wait）は検証しない（意図的）。</para>
/// </remarks>
public static class WaitEventsValidator
{
    private static readonly HashSet<string> ReservedEventNames = new(StringComparer.OrdinalIgnoreCase)
    {
        Fact.Completed,
        Fact.Failed,
        Fact.Cancelled,
        Fact.Joined
    };

    /// <summary>
    /// 指定状態の Wait.events を検証し、エラーを <paramref name="errors"/> に追加する。
    /// </summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="stateDef">状態定義。</param>
    /// <param name="stateNames">定義内の全状態名。</param>
    /// <param name="errors">エラー蓄積先（呼び出し側の <see cref="List{T}"/> など）。</param>
    public static void Validate(
        string stateName,
        StateDefinition stateDef,
        HashSet<string> stateNames,
        ICollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(stateDef);
        ArgumentNullException.ThrowIfNull(stateNames);
        ArgumentNullException.ThrowIfNull(errors);

        if (stateDef.Wait == null)
        {
            return;
        }

        var events = stateDef.Wait.Events;
        if (events.Count == 0)
        {
            errors.Add($"State '{stateName}' wait.events must not be empty.");
            return;
        }

        var seenEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (eventName, nextState) in events)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                errors.Add($"State '{stateName}' wait.events keys must be non-empty event names.");
                continue;
            }

            var trimmedEvent = eventName.Trim();
            if (!seenEventNames.Add(trimmedEvent))
            {
                errors.Add($"State '{stateName}' wait.events has duplicate event name '{trimmedEvent}'.");
            }

            if (ReservedEventNames.Contains(trimmedEvent))
            {
                errors.Add(
                    $"State '{stateName}' wait.events uses reserved event name '{trimmedEvent}'.");
            }

            if (string.IsNullOrWhiteSpace(nextState))
            {
                errors.Add(
                    $"State '{stateName}' wait.events['{trimmedEvent}'] must specify a next state.");
                continue;
            }

            var trimmedNext = nextState.Trim();
            if (!stateNames.Contains(trimmedNext))
            {
                errors.Add(
                    $"State '{stateName}' wait.events['{trimmedEvent}'] references unknown state '{trimmedNext}'.");
            }
            else if (string.Equals(trimmedNext, stateName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"State '{stateName}' wait.events['{trimmedEvent}'] must not self-transition.");
            }
        }
    }
}
