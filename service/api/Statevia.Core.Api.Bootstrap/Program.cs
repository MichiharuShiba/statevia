using Statevia.Core.Api.Bootstrap;

var exitCode = await BootstrapApp.RunAsync(args).ConfigureAwait(false);
return exitCode;
