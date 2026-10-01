var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "PixelHub API — dépôt de départ");

app.Run();
