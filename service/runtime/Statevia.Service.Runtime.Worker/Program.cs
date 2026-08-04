using Microsoft.Extensions.Hosting;
using Statevia.Service.Api.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddStateviaWorkerHost(builder.Configuration);

await builder.Build().RunAsync().ConfigureAwait(false);
