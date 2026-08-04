using Microsoft.Extensions.Hosting;
using Statevia.Runtime.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddStateviaSchedulerHost(builder.Configuration);

await builder.Build().RunAsync().ConfigureAwait(false);
