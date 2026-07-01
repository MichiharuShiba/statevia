using Statevia.Service.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddStateviaCoreApi(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ContractExceptionMiddleware>();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseRouting();
app.UseMiddleware<TraceContextEnrichmentMiddleware>();

app.UseStateviaOpenApi();

app.MapControllers();

app.Run();
