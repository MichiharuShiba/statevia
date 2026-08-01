using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Controllers;

namespace Statevia.Service.Api.Tests.Controllers;

/// <summary><see cref="EventsController"/> の配送 API。</summary>
public sealed class EventsControllerTests
{
    /// <summary>受理時は 204 を返し、key 省略は空文字としてサービスへ渡す。</summary>
    [Fact]
    public async Task Publish_ReturnsNoContent_AndPassesEmptyKeyWhenOmitted()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var controller = new EventsController(ingress);
        var request = new EventIngressRequest { Topic = "inventory.received" };

        // Act
        var result = await controller.Publish(request, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Equal("inventory.received", ingress.Topic);
        Assert.Equal("", ingress.Key);
    }

    /// <summary>一致 0 件でも 204 を返す（サービスが例外を投げない前提）。</summary>
    [Fact]
    public async Task Publish_WhenNoMatch_StillReturnsNoContent()
    {
        // Arrange
        var ingress = new CapturingEventIngressService();
        var controller = new EventsController(ingress);
        var request = new EventIngressRequest
        {
            Topic = "missing.topic",
            Key = "any"
        };

        // Act
        var result = await controller.Publish(request, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Equal(1, ingress.CallCount);
    }

    private sealed class CapturingEventIngressService : IEventIngressService
    {
        public string? Topic { get; private set; }
        public string? Key { get; private set; }
        public int CallCount { get; private set; }

        public Task PublishAsync(string topic, string key, CancellationToken ct)
        {
            Topic = topic;
            Key = key;
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
