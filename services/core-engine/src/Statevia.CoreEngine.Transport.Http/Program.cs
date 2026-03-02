var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// POST /internal/v1/decide はタスク 1.7 で追加
app.MapGet("/", () => Results.Ok(new { service = "Statevia.CoreEngine", status = "running" }));

app.Run();
