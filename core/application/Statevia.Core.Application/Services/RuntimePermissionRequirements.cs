namespace Statevia.Core.Application.Services;

/// <summary>
/// Runtime API（Definitions / Executions / Graphs）の global permission 要件。
/// </summary>
internal static class RuntimePermissionRequirements
{
    /// <summary>GET /v1/definitions*、GET /v1/graphs/*、GET …/schema/nodes、GET /v1/actions/schema*。</summary>
    public const string DefinitionsRead = WellKnownPermissionKeys.DefinitionsRead;

    /// <summary>POST /v1/definitions、PUT /v1/definitions/{id}。</summary>
    public const string DefinitionsWrite = WellKnownPermissionKeys.DefinitionsWrite;

    /// <summary>GET /v1/executions*（一覧・詳細・graph・state・events・stream）。</summary>
    public const string ExecutionsRead = WellKnownPermissionKeys.ExecutionsRead;

    /// <summary>POST /v1/executions、cancel、publish/resume。</summary>
    public const string ExecutionsWrite = WellKnownPermissionKeys.ExecutionsWrite;
}
