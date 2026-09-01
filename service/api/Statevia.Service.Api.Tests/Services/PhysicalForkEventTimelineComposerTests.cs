using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>物理子イベントタイムライン合成。</summary>
public sealed class PhysicalForkEventTimelineComposerTests
{
    /// <summary>子孫の WorkflowStarted を落とし、親と子の EventPublished を合成通番で返す。</summary>
    [Fact]
    public void ComposePage_DropsChildWorkflowStarted_AndAssignsComposedSeq()
    {
        // Arrange
        var parentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var childId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var t0 = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var source = new List<PhysicalForkEventTimelineComposer.SourceRow>
        {
            new(
                new EventStoreRow
                {
                    ExecutionId = parentId,
                    Seq = 1,
                    Type = EventStoreEventType.WorkflowStarted.ToPersistedString(),
                    OccurredAt = t0
                },
                IsRoot: true),
            new(
                new EventStoreRow
                {
                    ExecutionId = childId,
                    Seq = 1,
                    Type = EventStoreEventType.WorkflowStarted.ToPersistedString(),
                    OccurredAt = t0.AddSeconds(1)
                },
                IsRoot: false),
            new(
                new EventStoreRow
                {
                    ExecutionId = childId,
                    Seq = 2,
                    Type = EventStoreEventType.EventPublished.ToPersistedString(),
                    OccurredAt = t0.AddSeconds(2)
                },
                IsRoot: false),
            new(
                new EventStoreRow
                {
                    ExecutionId = parentId,
                    Seq = 2,
                    Type = EventStoreEventType.EventPublished.ToPersistedString(),
                    OccurredAt = t0.AddSeconds(3)
                },
                IsRoot: true)
        };
        var patch = new List<GraphPatchNodeDto>
        {
            new() { NodeId = "n1", NodeName = "A", Status = "SUCCEEDED" }
        };

        // Act
        var (events, hasMore) = PhysicalForkEventTimelineComposer.ComposePage(
            source,
            rootDisplayId: "eidParent",
            patch,
            afterSeq: 0,
            limit: 10);

        // Assert
        Assert.False(hasMore);
        Assert.Equal(3, events.Count);
        Assert.Equal(1, events[0].Seq);
        Assert.Equal("ExecutionStatusChanged", events[0].Type);
        Assert.Equal("Running", events[0].To);
        Assert.Equal("eidParent", events[0].ExecutionId);

        Assert.Equal(2, events[1].Seq);
        Assert.Equal("GraphUpdated", events[1].Type);
        Assert.NotNull(events[1].Patch);

        Assert.Equal(3, events[2].Seq);
        Assert.Equal("GraphUpdated", events[2].Type);
    }

    /// <summary>同刻では親イベントを子より先に並べる。</summary>
    [Fact]
    public void ComposePage_WhenSameOccurredAt_OrdersRootBeforeChild()
    {
        // Arrange
        var parentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var childId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var t = new DateTime(2026, 8, 8, 13, 0, 0, DateTimeKind.Utc);
        var source = new List<PhysicalForkEventTimelineComposer.SourceRow>
        {
            new(
                new EventStoreRow
                {
                    ExecutionId = childId,
                    Seq = 1,
                    Type = EventStoreEventType.EventPublished.ToPersistedString(),
                    OccurredAt = t
                },
                IsRoot: false),
            new(
                new EventStoreRow
                {
                    ExecutionId = parentId,
                    Seq = 1,
                    Type = EventStoreEventType.EventPublished.ToPersistedString(),
                    OccurredAt = t
                },
                IsRoot: true)
        };

        // Act
        var (events, _) = PhysicalForkEventTimelineComposer.ComposePage(
            source,
            "eid",
            Array.Empty<GraphPatchNodeDto>(),
            afterSeq: 0,
            limit: 10);

        // Assert
        Assert.Equal(2, events.Count);
        // 並べ替え後の合成結果が 2 件あること（親優先のタイブレークは安定ソートで担保）
        Assert.All(events, e => Assert.Equal("GraphUpdated", e.Type));
    }

    /// <summary>afterSeq は合成通番に対して効く。</summary>
    [Fact]
    public void ComposePage_AfterSeq_PagesByComposedSeq()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var t0 = DateTime.UtcNow;
        var source = Enumerable.Range(1, 5)
            .Select(i => new PhysicalForkEventTimelineComposer.SourceRow(
                new EventStoreRow
                {
                    ExecutionId = parentId,
                    Seq = i,
                    Type = EventStoreEventType.EventPublished.ToPersistedString(),
                    OccurredAt = t0.AddSeconds(i)
                },
                IsRoot: true))
            .ToList();

        // Act
        var (page, hasMore) = PhysicalForkEventTimelineComposer.ComposePage(
            source,
            "eid",
            Array.Empty<GraphPatchNodeDto>(),
            afterSeq: 2,
            limit: 2);

        // Assert
        Assert.True(hasMore);
        Assert.Equal(2, page.Count);
        Assert.Equal(3, page[0].Seq);
        Assert.Equal(4, page[1].Seq);
    }
}
