using System.CommandLine;
using Statevia.Service.Cli.Commands;

namespace Statevia.Service.Cli;

/// <summary>統合 <c>statevia</c> CLI のエントリポイント。</summary>
public static class Program
{
    /// <summary>CLI を実行する。</summary>
    /// <param name="args">コマンドライン引数。</param>
    public static Task<int> Main(string[] args)
    {
        var root = new RootCommand("Statevia platform CLI");
        root.AddCommand(DefinitionValidateCommand.Create());
        root.AddCommand(ModuleInstallCommand.Create());
        return root.InvokeAsync(args);
    }
}
