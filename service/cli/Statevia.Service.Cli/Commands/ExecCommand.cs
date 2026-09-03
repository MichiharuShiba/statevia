using Statevia.Service.Cli.Infrastructure;
using System.CommandLine;
using System.Text.Json;

namespace Statevia.Service.Cli.Commands;

/// <summary><c>statevia exec</c> 親コマンド（実行の運用コア）。</summary>
/// <remarks>親名は <c>exec</c> のみ。<c>execution</c> エイリアスは付けない。互換シム <c>POST .../events</c> は提供しない。</remarks>
public static class ExecCommand
{
    private const string ExecutionIdDescription = "Execution display id or UUID";

    /// <summary>コマンド定義を生成する。</summary>
    /// <returns>親 <c>exec</c>。</returns>
    public static Command Create()
    {
        var command = new Command("exec", "Execution start, inspect, waits, cancel, and resume");
        command.AddCommand(CreateStart());
        command.AddCommand(CreateList());
        command.AddCommand(CreateGet());
        command.AddCommand(CreateWaits());
        command.AddCommand(CreateCancel());
        command.AddCommand(CreateResume());
        return command;
    }

    private static Command CreateStart()
    {
        var definitionIdArgument = new Argument<string>("definition-id", "Definition display id or UUID");
        var versionOption = new Option<int?>("--version", "Definition version number");
        var versionIdOption = new Option<string?>("--version-id", "Definition version UUID");
        var inputOption = new Option<FileInfo?>("--input", "JSON file for start input");
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var idempotencyOption = CliRuntimeCommandSupport.CreateIdempotencyKeyOption();
        var start = new Command("start", "Start an execution (POST /v1/executions)")
        {
            definitionIdArgument,
            versionOption,
            versionIdOption,
            inputOption,
            apiBaseOption,
            tenantOption,
            apiKeyOption,
            idempotencyOption,
        };
        var binder = new CliValueBinder<StartArgs>(ctx => new StartArgs(
            ctx.ParseResult.GetValueForArgument(definitionIdArgument),
            ctx.ParseResult.GetValueForOption(versionOption),
            ctx.ParseResult.GetValueForOption(versionIdOption),
            ctx.ParseResult.GetValueForOption(inputOption),
            ctx.ParseResult.GetValueForOption(apiBaseOption),
            ctx.ParseResult.GetValueForOption(tenantOption),
            ctx.ParseResult.GetValueForOption(apiKeyOption),
            ctx.ParseResult.GetValueForOption(idempotencyOption)));
        start.SetHandler(StartAsync, binder);
        return start;
    }

    private static Command CreateList()
    {
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var limitOption = CliRuntimeCommandSupport.CreateLimitOption();
        var offsetOption = CliRuntimeCommandSupport.CreateOffsetOption();
        var statusOption = new Option<string?>("--status", "Filter by execution status");
        var nameOption = new Option<string?>("--name", "Filter by execution display id");
        var definitionIdOption = new Option<string?>("--definition-id", "Filter by definition id");
        var sortByOption = new Option<string?>("--sort-by", "Sort field (updatedAt or displayId)");
        var sortOrderOption = new Option<string?>("--sort-order", "Sort order (asc or desc)");
        var list = new Command("list", "List executions (GET /v1/executions)")
        {
            apiBaseOption,
            tenantOption,
            apiKeyOption,
            limitOption,
            offsetOption,
            statusOption,
            nameOption,
            definitionIdOption,
            sortByOption,
            sortOrderOption,
        };
        var binder = new CliValueBinder<ListArgs>(ctx => new ListArgs(
            ctx.ParseResult.GetValueForOption(apiBaseOption),
            ctx.ParseResult.GetValueForOption(tenantOption),
            ctx.ParseResult.GetValueForOption(apiKeyOption),
            ctx.ParseResult.GetValueForOption(limitOption),
            ctx.ParseResult.GetValueForOption(offsetOption),
            ctx.ParseResult.GetValueForOption(statusOption),
            ctx.ParseResult.GetValueForOption(nameOption),
            ctx.ParseResult.GetValueForOption(definitionIdOption),
            ctx.ParseResult.GetValueForOption(sortByOption),
            ctx.ParseResult.GetValueForOption(sortOrderOption)));
        list.SetHandler(ListAsync, binder);
        return list;
    }

    private static Command CreateGet()
    {
        var idArgument = new Argument<string>("id", ExecutionIdDescription);
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var get = new Command("get", "Get an execution (GET /v1/executions/{id})")
        {
            idArgument,
            apiBaseOption,
            tenantOption,
            apiKeyOption,
        };
        var binder = new CliValueBinder<IdArgs>(ctx => new IdArgs(
            ctx.ParseResult.GetValueForArgument(idArgument),
            ctx.ParseResult.GetValueForOption(apiBaseOption),
            ctx.ParseResult.GetValueForOption(tenantOption),
            ctx.ParseResult.GetValueForOption(apiKeyOption),
            IdempotencyKey: null));
        get.SetHandler(GetAsync, binder);
        return get;
    }

    private static Command CreateWaits()
    {
        var idArgument = new Argument<string>("id", ExecutionIdDescription);
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var waits = new Command("waits", "List active waits (GET /v1/executions/{id}/waits)")
        {
            idArgument,
            apiBaseOption,
            tenantOption,
            apiKeyOption,
        };
        var binder = new CliValueBinder<IdArgs>(ctx => new IdArgs(
            ctx.ParseResult.GetValueForArgument(idArgument),
            ctx.ParseResult.GetValueForOption(apiBaseOption),
            ctx.ParseResult.GetValueForOption(tenantOption),
            ctx.ParseResult.GetValueForOption(apiKeyOption),
            IdempotencyKey: null));
        waits.SetHandler(WaitsAsync, binder);
        return waits;
    }

    private static Command CreateCancel()
    {
        var idArgument = new Argument<string>("id", ExecutionIdDescription);
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var idempotencyOption = CliRuntimeCommandSupport.CreateIdempotencyKeyOption();
        var cancel = new Command("cancel", "Cancel an execution (POST /v1/executions/{id}/cancel)")
        {
            idArgument,
            apiBaseOption,
            tenantOption,
            apiKeyOption,
            idempotencyOption,
        };
        var binder = new CliValueBinder<IdArgs>(ctx => new IdArgs(
            ctx.ParseResult.GetValueForArgument(idArgument),
            ctx.ParseResult.GetValueForOption(apiBaseOption),
            ctx.ParseResult.GetValueForOption(tenantOption),
            ctx.ParseResult.GetValueForOption(apiKeyOption),
            ctx.ParseResult.GetValueForOption(idempotencyOption)));
        cancel.SetHandler(CancelAsync, binder);
        return cancel;
    }

    private static Command CreateResume()
    {
        var idArgument = new Argument<string>("id", ExecutionIdDescription);
        var nodeOption = new Option<string>("--node", "Runtime node id") { IsRequired = true };
        var eventOption = new Option<string>("--event", "Wait resume key (event name)") { IsRequired = true };
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var idempotencyOption = CliRuntimeCommandSupport.CreateIdempotencyKeyOption();
        var resume = new Command("resume", "Resume a wait node (POST /v1/executions/{id}/nodes/{nodeId}/resume)")
        {
            idArgument,
            nodeOption,
            eventOption,
            apiBaseOption,
            tenantOption,
            apiKeyOption,
            idempotencyOption,
        };
        var binder = new CliValueBinder<ResumeArgs>(ctx => new ResumeArgs(
            ctx.ParseResult.GetValueForArgument(idArgument),
            ctx.ParseResult.GetValueForOption(nodeOption),
            ctx.ParseResult.GetValueForOption(eventOption),
            ctx.ParseResult.GetValueForOption(apiBaseOption),
            ctx.ParseResult.GetValueForOption(tenantOption),
            ctx.ParseResult.GetValueForOption(apiKeyOption),
            ctx.ParseResult.GetValueForOption(idempotencyOption)));
        resume.SetHandler(ResumeAsync, binder);
        return resume;
    }

    private static async Task<int> StartAsync(StartArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.DefinitionId))
        {
            await Console.Error.WriteLineAsync("definition-id is required.").ConfigureAwait(false);
            return 1;
        }

        JsonElement? input = null;
        if (args.InputFile is not null)
        {
            if (!args.InputFile.Exists)
            {
                await Console.Error
                    .WriteLineAsync($"File not found: {args.InputFile.FullName}")
                    .ConfigureAwait(false);
                return 1;
            }

            try
            {
                var json = await File.ReadAllTextAsync(args.InputFile.FullName).ConfigureAwait(false);
                input = JsonSerializer.Deserialize<JsonElement>(json);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
                return 1;
            }
        }

        var session = await CliRuntimeCommandSupport
            .TryCreateSessionAsync(args.ApiBase, args.Tenant, args.ApiKey, args.IdempotencyKey)
            .ConfigureAwait(false);
        if (session is null)
            return 1;

        var versionId = string.IsNullOrWhiteSpace(args.VersionId) ? null : args.VersionId.Trim();
        var body = new StartBody(args.DefinitionId.Trim(), args.Version, versionId, input);
        return await CliRuntimeCommandSupport
            .SendAndWriteAsync(
                session,
                new CliApiSendRequest(
                    HttpMethod.Post,
                    "v1/executions",
                    Body: body,
                    IdempotencyKey: session.IdempotencyKey))
            .ConfigureAwait(false);
    }

    private static async Task<int> ListAsync(ListArgs args)
    {
        if (!await CliRuntimeCommandSupport.ValidatePagingAsync(args.Limit, args.Offset).ConfigureAwait(false))
            return 1;

        var session = await CliRuntimeCommandSupport
            .TryCreateSessionAsync(args.ApiBase, args.Tenant, args.ApiKey, idempotencyKey: null)
            .ConfigureAwait(false);
        if (session is null)
            return 1;

        var query = new List<KeyValuePair<string, string?>>
        {
            new("limit", args.Limit.ToString()),
            new("offset", args.Offset.ToString()),
            new("status", args.Status),
            new("name", args.Name),
            new("definitionId", args.DefinitionId),
            new("sortBy", args.SortBy),
            new("sortOrder", args.SortOrder),
        };
        return await CliRuntimeCommandSupport
            .SendAndWriteAsync(
                session,
                new CliApiSendRequest(HttpMethod.Get, "v1/executions", Query: query))
            .ConfigureAwait(false);
    }

    private static async Task<int> GetAsync(IdArgs args) =>
        await SendIdAsync(HttpMethod.Get, args, "v1/executions/{0}", mutation: false).ConfigureAwait(false);

    private static async Task<int> WaitsAsync(IdArgs args) =>
        await SendIdAsync(HttpMethod.Get, args, "v1/executions/{0}/waits", mutation: false).ConfigureAwait(false);

    private static async Task<int> CancelAsync(IdArgs args) =>
        await SendIdAsync(HttpMethod.Post, args, "v1/executions/{0}/cancel", mutation: true).ConfigureAwait(false);

    private static async Task<int> ResumeAsync(ResumeArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Id)
            || string.IsNullOrWhiteSpace(args.NodeId)
            || string.IsNullOrWhiteSpace(args.EventName))
        {
            await Console.Error.WriteLineAsync("id, --node, and --event are required.").ConfigureAwait(false);
            return 1;
        }

        var executionId = await CliRuntimeCommandSupport
            .TryEncodePathSegmentAsync(args.Id, "id")
            .ConfigureAwait(false);
        if (executionId is null)
            return 1;

        var nodeId = await CliRuntimeCommandSupport
            .TryEncodePathSegmentAsync(args.NodeId, "--node")
            .ConfigureAwait(false);
        if (nodeId is null)
            return 1;

        var session = await CliRuntimeCommandSupport
            .TryCreateSessionAsync(args.ApiBase, args.Tenant, args.ApiKey, args.IdempotencyKey)
            .ConfigureAwait(false);
        if (session is null)
            return 1;

        var path = $"v1/executions/{executionId}/nodes/{nodeId}/resume";
        return await CliRuntimeCommandSupport
            .SendAndWriteAsync(
                session,
                new CliApiSendRequest(
                    HttpMethod.Post,
                    path,
                    Body: new ResumeBody(args.EventName.Trim()),
                    IdempotencyKey: session.IdempotencyKey))
            .ConfigureAwait(false);
    }

    private static async Task<int> SendIdAsync(HttpMethod method, IdArgs args, string pathTemplate, bool mutation)
    {
        if (string.IsNullOrWhiteSpace(args.Id))
        {
            await Console.Error.WriteLineAsync("id is required.").ConfigureAwait(false);
            return 1;
        }

        var executionId = await CliRuntimeCommandSupport
            .TryEncodePathSegmentAsync(args.Id, "id")
            .ConfigureAwait(false);
        if (executionId is null)
            return 1;

        var session = await CliRuntimeCommandSupport
            .TryCreateSessionAsync(
                args.ApiBase,
                args.Tenant,
                args.ApiKey,
                mutation ? args.IdempotencyKey : null)
            .ConfigureAwait(false);
        if (session is null)
            return 1;

        var path = string.Format(pathTemplate, executionId);
        return await CliRuntimeCommandSupport
            .SendAndWriteAsync(
                session,
                new CliApiSendRequest(
                    method,
                    path,
                    IdempotencyKey: mutation ? session.IdempotencyKey : null))
            .ConfigureAwait(false);
    }

    private sealed record StartArgs(
        string DefinitionId,
        int? Version,
        string? VersionId,
        FileInfo? InputFile,
        string? ApiBase,
        string? Tenant,
        string? ApiKey,
        string? IdempotencyKey);

    private sealed record ListArgs(
        string? ApiBase,
        string? Tenant,
        string? ApiKey,
        int Limit,
        int Offset,
        string? Status,
        string? Name,
        string? DefinitionId,
        string? SortBy,
        string? SortOrder);

    private sealed record IdArgs(
        string Id,
        string? ApiBase,
        string? Tenant,
        string? ApiKey,
        string? IdempotencyKey);

    private sealed record ResumeArgs(
        string Id,
        string? NodeId,
        string? EventName,
        string? ApiBase,
        string? Tenant,
        string? ApiKey,
        string? IdempotencyKey);

    private sealed record StartBody(
        string DefinitionId,
        int? DefinitionVersion,
        string? DefinitionVersionId,
        JsonElement? Input);

    private sealed record ResumeBody(string ResumeKey);
}
