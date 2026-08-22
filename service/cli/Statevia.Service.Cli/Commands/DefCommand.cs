using System.CommandLine;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Statevia.Service.Cli.Infrastructure;

namespace Statevia.Service.Cli.Commands;

/// <summary><c>statevia def</c> 親コマンド（validate と catalog 運用コア）。</summary>
/// <remarks>親名は <c>def</c> のみ。<c>definition</c> エイリアスは付けない。</remarks>
public static class DefCommand
{
    private const string DefinitionIdDescription = "Definition display id or UUID";

    /// <summary>コマンド定義を生成する。</summary>
    /// <returns>親 <c>def</c>。</returns>
    public static Command Create()
    {
        var command = new Command("def", "Definition catalog and local validation");
        command.AddCommand(CreateValidate());
        command.AddCommand(CreateCreate());
        command.AddCommand(CreatePublish());
        command.AddCommand(CreateList());
        command.AddCommand(CreateGet());
        command.AddCommand(CreateDelete());
        command.AddCommand(CreateRestore());
        return command;
    }

    private static Command CreateValidate()
    {
        var yamlArgument = new Argument<FileInfo>("yaml-file", "Workflow definition YAML file");
        var validate = new Command("validate", "Validate a workflow definition YAML file")
        {
            yamlArgument,
        };
        validate.SetHandler(ValidateAsync, yamlArgument);
        return validate;
    }

    private static Command CreateCreate()
    {
        var fileOption = CreateFileOption();
        var nameOption = CreateNameOption();
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var idempotencyOption = CliRuntimeCommandSupport.CreateIdempotencyKeyOption();
        var create = new Command("create", "Register a definition (POST /v1/definitions)")
        {
            fileOption,
            nameOption,
            apiBaseOption,
            tenantOption,
            apiKeyOption,
            idempotencyOption,
        };
        var binder = new CliValueBinder<CreateArgs>(ctx => new CreateArgs(
            ctx.ParseResult.GetValueForOption(fileOption),
            ctx.ParseResult.GetValueForOption(nameOption),
            ctx.ParseResult.GetValueForOption(apiBaseOption),
            ctx.ParseResult.GetValueForOption(tenantOption),
            ctx.ParseResult.GetValueForOption(apiKeyOption),
            ctx.ParseResult.GetValueForOption(idempotencyOption)));
        create.SetHandler(CreateAsync, binder);
        return create;
    }

    private static Command CreatePublish()
    {
        var idArgument = new Argument<string>("id", DefinitionIdDescription);
        var fileOption = CreateFileOption();
        var nameOption = CreateNameOption();
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var idempotencyOption = CliRuntimeCommandSupport.CreateIdempotencyKeyOption();
        var publish = new Command("publish", "Append a definition version (PUT /v1/definitions/{id})")
        {
            idArgument,
            fileOption,
            nameOption,
            apiBaseOption,
            tenantOption,
            apiKeyOption,
            idempotencyOption,
        };
        var binder = new CliValueBinder<PublishArgs>(ctx => new PublishArgs(
            ctx.ParseResult.GetValueForArgument(idArgument),
            ctx.ParseResult.GetValueForOption(fileOption),
            ctx.ParseResult.GetValueForOption(nameOption),
            ctx.ParseResult.GetValueForOption(apiBaseOption),
            ctx.ParseResult.GetValueForOption(tenantOption),
            ctx.ParseResult.GetValueForOption(apiKeyOption),
            ctx.ParseResult.GetValueForOption(idempotencyOption)));
        publish.SetHandler(PublishAsync, binder);
        return publish;
    }

    private static Command CreateList()
    {
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var limitOption = CliRuntimeCommandSupport.CreateLimitOption();
        var offsetOption = CliRuntimeCommandSupport.CreateOffsetOption();
        var nameOption = new Option<string?>("--name", "Filter by definition name");
        var sortByOption = new Option<string?>("--sort-by", "Sort field (createdAt or name)");
        var sortOrderOption = new Option<string?>("--sort-order", "Sort order (asc or desc)");
        var includeDeletedOption = new Option<bool>("--include-deleted", "Include logically deleted definitions");
        var list = new Command("list", "List definitions (GET /v1/definitions)")
        {
            apiBaseOption,
            tenantOption,
            apiKeyOption,
            limitOption,
            offsetOption,
            nameOption,
            sortByOption,
            sortOrderOption,
            includeDeletedOption,
        };
        var binder = new CliValueBinder<ListArgs>(ctx => new ListArgs(
            ctx.ParseResult.GetValueForOption(apiBaseOption),
            ctx.ParseResult.GetValueForOption(tenantOption),
            ctx.ParseResult.GetValueForOption(apiKeyOption),
            ctx.ParseResult.GetValueForOption(limitOption),
            ctx.ParseResult.GetValueForOption(offsetOption),
            ctx.ParseResult.GetValueForOption(nameOption),
            ctx.ParseResult.GetValueForOption(sortByOption),
            ctx.ParseResult.GetValueForOption(sortOrderOption),
            ctx.ParseResult.GetValueForOption(includeDeletedOption)));
        list.SetHandler(ListAsync, binder);
        return list;
    }

    private static Command CreateGet()
    {
        var idArgument = new Argument<string>("id", DefinitionIdDescription);
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var get = new Command("get", "Get a definition (GET /v1/definitions/{id})")
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

    private static Command CreateDelete()
    {
        var idArgument = new Argument<string>("id", DefinitionIdDescription);
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var idempotencyOption = CliRuntimeCommandSupport.CreateIdempotencyKeyOption();
        var delete = new Command("delete", "Logically delete a definition (DELETE /v1/definitions/{id})")
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
        delete.SetHandler(DeleteAsync, binder);
        return delete;
    }

    private static Command CreateRestore()
    {
        var idArgument = new Argument<string>("id", DefinitionIdDescription);
        var apiBaseOption = CliRuntimeCommandSupport.CreateApiBaseOption();
        var tenantOption = CliRuntimeCommandSupport.CreateTenantOption();
        var apiKeyOption = CliRuntimeCommandSupport.CreateApiKeyOption();
        var idempotencyOption = CliRuntimeCommandSupport.CreateIdempotencyKeyOption();
        var restore = new Command("restore", "Restore a logically deleted definition (POST /v1/definitions/{id}/restore)")
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
        restore.SetHandler(RestoreAsync, binder);
        return restore;
    }

    private static Option<FileInfo> CreateFileOption() =>
        new("--file", "Workflow definition YAML file") { IsRequired = true };

    private static Option<string?> CreateNameOption() =>
        new("--name", "Definition name. Overrides the workflow name in YAML when both are set");

    private static async Task<int> ValidateAsync(FileInfo yamlFile)
    {
        if (!yamlFile.Exists)
        {
            await Console.Error.WriteLineAsync($"File not found: {yamlFile.FullName}").ConfigureAwait(false);
            return 1;
        }

        var loader = new StateWorkflowDefinitionLoader();
        var content = await File.ReadAllTextAsync(yamlFile.FullName).ConfigureAwait(false);

        try
        {
            var definition = loader.Load(content);
            await Console.Out.WriteLineAsync($"Loaded workflow: {definition.Name}").ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"States: {string.Join(", ", definition.States.Keys)}").ConfigureAwait(false);

            var result = DefinitionValidator.Validate(definition);
            if (!result.IsValid)
            {
                await Console.Error.WriteLineAsync("Validation failed:").ConfigureAwait(false);
                foreach (var error in result.Errors)
                    await Console.Error.WriteLineAsync("  - " + error).ConfigureAwait(false);

                return 1;
            }

            await Console.Out.WriteLineAsync("Validation: OK").ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> CreateAsync(CreateArgs args)
    {
        var prepared = await TryReadYamlAndNameAsync(args.File, args.Name).ConfigureAwait(false);
        if (prepared is null)
            return 1;

        var session = await CliRuntimeCommandSupport
            .TryCreateSessionAsync(args.ApiBase, args.Tenant, args.ApiKey, args.IdempotencyKey)
            .ConfigureAwait(false);
        if (session is null)
            return 1;

        return await CliRuntimeCommandSupport
            .SendAndWriteAsync(
                session,
                new CliApiSendRequest(
                    HttpMethod.Post,
                    "v1/definitions",
                    Body: new CreateOrPublishBody(prepared.Value.Name, prepared.Value.Yaml),
                    IdempotencyKey: session.IdempotencyKey))
            .ConfigureAwait(false);
    }

    private static async Task<int> PublishAsync(PublishArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Id))
        {
            await Console.Error.WriteLineAsync("id is required.").ConfigureAwait(false);
            return 1;
        }

        var definitionId = await CliRuntimeCommandSupport
            .TryEncodePathSegmentAsync(args.Id, "id")
            .ConfigureAwait(false);
        if (definitionId is null)
            return 1;

        var prepared = await TryReadYamlAndNameAsync(args.File, args.Name).ConfigureAwait(false);
        if (prepared is null)
            return 1;

        var session = await CliRuntimeCommandSupport
            .TryCreateSessionAsync(args.ApiBase, args.Tenant, args.ApiKey, args.IdempotencyKey)
            .ConfigureAwait(false);
        if (session is null)
            return 1;

        return await CliRuntimeCommandSupport
            .SendAndWriteAsync(
                session,
                new CliApiSendRequest(
                    HttpMethod.Put,
                    $"v1/definitions/{definitionId}",
                    Body: new CreateOrPublishBody(prepared.Value.Name, prepared.Value.Yaml),
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
            new("name", args.Name),
            new("sortBy", args.SortBy),
            new("sortOrder", args.SortOrder),
        };
        if (args.IncludeDeleted)
            query.Add(new("includeDeleted", "true"));

        return await CliRuntimeCommandSupport
            .SendAndWriteAsync(
                session,
                new CliApiSendRequest(HttpMethod.Get, "v1/definitions", Query: query))
            .ConfigureAwait(false);
    }

    private static async Task<int> GetAsync(IdArgs args) =>
        await SendIdAsync(HttpMethod.Get, args, "v1/definitions/{0}", mutation: false).ConfigureAwait(false);

    private static async Task<int> DeleteAsync(IdArgs args) =>
        await SendIdAsync(HttpMethod.Delete, args, "v1/definitions/{0}", mutation: true).ConfigureAwait(false);

    private static async Task<int> RestoreAsync(IdArgs args) =>
        await SendIdAsync(HttpMethod.Post, args, "v1/definitions/{0}/restore", mutation: true).ConfigureAwait(false);

    private static async Task<int> SendIdAsync(HttpMethod method, IdArgs args, string pathTemplate, bool mutation)
    {
        if (string.IsNullOrWhiteSpace(args.Id))
        {
            await Console.Error.WriteLineAsync("id is required.").ConfigureAwait(false);
            return 1;
        }

        var definitionId = await CliRuntimeCommandSupport
            .TryEncodePathSegmentAsync(args.Id, "id")
            .ConfigureAwait(false);
        if (definitionId is null)
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

        var path = string.Format(pathTemplate, definitionId);
        return await CliRuntimeCommandSupport
            .SendAndWriteAsync(
                session,
                new CliApiSendRequest(
                    method,
                    path,
                    IdempotencyKey: mutation ? session.IdempotencyKey : null))
            .ConfigureAwait(false);
    }

    private static async Task<(string Name, string Yaml)?> TryReadYamlAndNameAsync(FileInfo? file, string? nameFlag)
    {
        if (file is null || !file.Exists)
        {
            await Console.Error
                .WriteLineAsync(file is null ? "--file is required." : $"File not found: {file.FullName}")
                .ConfigureAwait(false);
            return null;
        }

        string yaml;
        try
        {
            yaml = await File.ReadAllTextAsync(file.FullName).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(nameFlag))
            return (nameFlag.Trim(), yaml);

        try
        {
            var loadedName = new StateWorkflowDefinitionLoader().Load(yaml).Name;
            if (string.IsNullOrWhiteSpace(loadedName))
            {
                await Console.Error
                    .WriteLineAsync("Definition name is required (--name or workflow name in YAML).")
                    .ConfigureAwait(false);
                return null;
            }

            return (loadedName, yaml);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return null;
        }
    }

    private sealed record CreateArgs(
        FileInfo? File,
        string? Name,
        string? ApiBase,
        string? Tenant,
        string? ApiKey,
        string? IdempotencyKey);

    private sealed record PublishArgs(
        string Id,
        FileInfo? File,
        string? Name,
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
        string? Name,
        string? SortBy,
        string? SortOrder,
        bool IncludeDeleted);

    private sealed record IdArgs(
        string Id,
        string? ApiBase,
        string? Tenant,
        string? ApiKey,
        string? IdempotencyKey);

    private sealed record CreateOrPublishBody(string Name, string Yaml);
}
