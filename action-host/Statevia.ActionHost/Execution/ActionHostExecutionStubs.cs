using Statevia.Core.Engine.Abstractions;

namespace Statevia.ActionHost.Execution;

/// <summary>OutOfProcess 実行用の空 <see cref="IEventProvider"/>（Wait / Signal は未サポート）。</summary>
internal sealed class EmptyEventProvider : IEventProvider
{
    /// <inheritdoc />
    public Task WaitAsync(string eventName, CancellationToken ct) =>
        Task.FromException(new NotSupportedException(
            "Event wait is not supported in Action Host OutOfProcess execution."));

    /// <inheritdoc />
    public void Signal(string signalName) =>
        throw new NotSupportedException(
            "Event signal is not supported in Action Host OutOfProcess execution.");

    /// <inheritdoc />
    public void PublishTopic(string topic, object? payloadSummary) =>
        throw new NotSupportedException(
            "Topic publish is not supported in Action Host OutOfProcess execution.");
}

/// <summary>OutOfProcess 実行用の空 <see cref="IReadOnlyStateStore"/>。</summary>
internal sealed class EmptyStateStore : IReadOnlyStateStore
{
    /// <inheritdoc />
    public bool TryGetOutput(string stateName, out object? output)
    {
        output = null;
        return false;
    }
}
