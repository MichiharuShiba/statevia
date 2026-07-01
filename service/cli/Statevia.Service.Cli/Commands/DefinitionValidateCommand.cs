using System.CommandLine;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;

namespace Statevia.Service.Cli.Commands;

/// <summary><c>statevia definition validate</c> サブコマンド。</summary>
public static class DefinitionValidateCommand
{
    /// <summary>コマンド定義を生成する。</summary>
    public static Command Create()
    {
        var yamlArgument = new Argument<FileInfo>("yaml-file", "Workflow definition YAML file");
        var command = new Command("definition", "Definition utilities");
        var validate = new Command("validate", "Validate a workflow definition YAML file")
        {
            yamlArgument,
        };
        validate.SetHandler(ValidateAsync, yamlArgument);
        command.AddCommand(validate);
        return command;
    }

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
                {
                    await Console.Error.WriteLineAsync("  - " + error).ConfigureAwait(false);
                }

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
}
