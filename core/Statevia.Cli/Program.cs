using Statevia.Core.Definition;
using Statevia.Core.Definition.Validation;

if (args.Length < 1)
{
    Console.WriteLine("Usage: Statevia.Cli <yaml-file>");
    return 1;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.WriteLine($"File not found: {path}");
    return 1;
}

var loader = new DefinitionLoader();
var content = File.ReadAllText(path);

try
{
    var def = loader.Load(content);
    Console.WriteLine($"Loaded workflow: {def.Workflow.Name}");
    Console.WriteLine($"States: {string.Join(", ", def.States.Keys)}");

    var validator = new DefinitionValidator();
    var result = validator.Validate(def);
    if (!result.IsValid)
    {
        Console.WriteLine("Validation failed:");
        foreach (var e in result.Errors)
        {
            Console.WriteLine("  - " + e);
        }
        return 1;
    }
    Console.WriteLine("Validation: OK");

    return 0;
}
#pragma warning disable CA1031 // Catch specific exceptions in entry point for user-facing error message
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}
#pragma warning restore CA1031
