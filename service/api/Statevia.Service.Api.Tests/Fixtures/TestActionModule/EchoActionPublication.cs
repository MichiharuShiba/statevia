using System.Text.Json;
using Statevia.Core.Actions.Abstractions.Publication;

namespace Statevia.Service.Api.Tests.Fixtures.TestActionModule;

/// <summary>テスト Module <c>test.module.echo</c> の ActionPublication。</summary>
internal static class EchoActionPublication
{
    /// <summary>echo action の Publication を生成する。</summary>
    /// <param name="actionId">canonical actionId。</param>
    public static ActionPublication Create(string actionId) =>
        new(
            new ActionDescriptor(
                actionId,
                "1.0.0",
                "Echo",
                Category: "Test"),
            new ActionSchemaBundle(
                JsonDocument.Parse(
                    """
                    {
                      "type": "object",
                      "additionalProperties": true
                    }
                    """),
                JsonDocument.Parse(
                    """
                    {
                      "type":"object",
                      "additionalProperties":true
                    }
                    """)));
}
