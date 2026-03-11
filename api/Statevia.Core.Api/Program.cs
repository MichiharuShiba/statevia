using Microsoft.EntityFrameworkCore;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);

// IWorkflowEngine を DI でシングルトン登録（計画 3.3）
builder.Services.AddSingleton<IWorkflowEngine>(_ => new WorkflowEngine());

// DbContext（EF Core + PostgreSQL）
var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=localhost;Database=statevia;Username=statevia;Password=statevia";
builder.Services.AddDbContextFactory<CoreDbContext>(options =>
    options.UseNpgsql(conn, o => o.MigrationsHistoryTable("__ef_migrations_history")));

builder.Services.AddScoped<IDisplayIdService, DisplayIdServiceImpl>();
builder.Services.AddSingleton<IDefinitionCompilerService, DefinitionCompilerService>();

var app = builder.Build();

app.MapControllers();
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" }));

app.Run();
