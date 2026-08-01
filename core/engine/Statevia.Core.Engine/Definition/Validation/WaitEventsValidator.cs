using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.FSM;

namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// Wait State の <c>events</c> / <c>subscribe</c> を Level1 で検証する。
/// </summary>
/// <remarks>
/// <para>Signal（events）と Subscribe（subscribe）は排他。どちらか一方が必須。</para>
/// <para>key は未指定を空文字に正規化する前提で、空 topic は拒否する。</para>
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
    /// 指定状態の Wait を検証し、エラーを <paramref name="errors"/> に追加する。
    /// </summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="stateDef">状態定義。</param>
    /// <param name="stateNames">定義内の全状態名。</param>
    /// <param name="errors">エラー蓄積先。</param>
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

        var hasEvents = stateDef.Wait.Events.Count > 0;
        var hasSubscribe = stateDef.Wait.Subscribe.Count > 0;
        if (hasEvents && hasSubscribe)
        {
            errors.Add($"State '{stateName}' wait cannot specify both events and subscribe.");
            return;
        }

        if (!hasEvents && !hasSubscribe)
        {
            errors.Add($"State '{stateName}' wait must specify events or subscribe.");
            return;
        }

        if (hasSubscribe)
        {
            ValidateSubscribe(stateName, stateDef.Wait.Subscribe, stateNames, errors);
            return;
        }

        ValidateEvents(stateName, stateDef.Wait.Events, stateNames, errors);
    }

    private static void ValidateEvents(
        string stateName,
        IReadOnlyDictionary<string, string> events,
        HashSet<string> stateNames,
        ICollection<string> errors)
    {
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

            if (ReservedEventNames.Contains(trimmedEvent)
                || trimmedEvent.StartsWith(WaitSubscribeEventNames.ReservedNamespacePrefix, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"State '{stateName}' wait.events uses reserved event name '{trimmedEvent}'.");
            }

            ValidateNextState(stateName, $"wait.events['{trimmedEvent}']", nextState, stateNames, errors);
        }
    }

    private static void ValidateSubscribe(
        string stateName,
        IReadOnlyList<WaitSubscribeEntry> subscribe,
        HashSet<string> stateNames,
        ICollection<string> errors)
    {
        for (var i = 0; i < subscribe.Count; i++)
        {
            var entry = subscribe[i];
            if (string.IsNullOrWhiteSpace(entry.Topic))
            {
                errors.Add($"State '{stateName}' wait.subscribe[{i}].topic must not be empty.");
            }

            ValidateNextState(stateName, $"wait.subscribe[{i}]", entry.Next, stateNames, errors);
        }
    }

    private static void ValidateNextState(
        string stateName,
        string path,
        string? nextState,
        HashSet<string> stateNames,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(nextState))
        {
            errors.Add($"State '{stateName}' {path} must specify a next state.");
            return;
        }

        var trimmedNext = nextState.Trim();
        if (!stateNames.Contains(trimmedNext))
        {
            errors.Add(
                $"State '{stateName}' {path} references unknown state '{trimmedNext}'.");
        }
        else if (string.Equals(trimmedNext, stateName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"State '{stateName}' {path} must not self-transition.");
        }
    }
}
