using Microsoft.EntityFrameworkCore;
using PixelHub.Api.Data;
using PixelHub.Api.Models;
using MongoDB.Driver;
using PixelHub.Api.Repositories;
using StackExchange.Redis;
using Neo4j.Driver;
using PixelHub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PixelHubContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// Le MongoClient est thread-safe et gère son propre pool de connexions :
// on l'enregistre donc en singleton.
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
      .GetDatabase(builder.Configuration["Mongo:Database"]));

builder.Services.AddSingleton<MongoGameCatalog>();
builder.Services.AddSingleton<IGameCatalog>(sp =>
    new CachedGameCatalog(
        sp.GetRequiredService<MongoGameCatalog>(),
        sp.GetRequiredService<IConnectionMultiplexer>()));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSingleton<ILeaderboard, RedisLeaderboard>();

// ---------- Neo4j (séance 4) ----------
// Le driver est partagé (singleton) ; la session, elle, est jetable, une par opération (Q12).
builder.Services.AddSingleton<IDriver>(_ =>
    GraphDatabase.Driver(
        builder.Configuration["Neo4j:Uri"],
        AuthTokens.Basic(
            builder.Configuration["Neo4j:User"],
            builder.Configuration["Neo4j:Password"])));

builder.Services.AddSingleton<ISocialGraph, Neo4jSocialGraph>();

// ---------- Séance 4 bis — l'application à quatre moteurs ----------
builder.Services.AddSingleton<RedisSales>();
// Scoped, et non singleton : ces services utilisent le DbContext, qui est scoped (une instance par requête).
builder.Services.AddScoped<ShopService>();
builder.Services.AddScoped<PlayerProfileService>();

var app = builder.Build();

// Crée la base et les tables au démarrage.
// Suffisant en TP ; dans un vrai projet, on utilise les migrations EF Core.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PixelHubContext>();
    db.Database.EnsureCreated();

    if (!db.Players.Any())
    {
        db.Players.AddRange(
            new Player { Pseudo = "Nova", Coins = 1200 },
            new Player { Pseudo = "Krayz", Coins = 350 },
            new Player { Pseudo = "Ombre", Coins = 90 }
        );
        db.SaveChanges();
    }
}

app.MapGet("/", () => "PixelHub API — séance 1 OK");

app.MapGet("/players", async (PixelHubContext db) =>
    await db.Players.ToListAsync());

app.MapGet("/games", async (IGameCatalog catalog) =>
    await catalog.GetAllAsync());

app.MapGet("/games/genre/{genre}", async (string genre, IGameCatalog catalog) =>
    await catalog.GetByGenreAsync(genre));

app.MapGet("/games/top/{count:int}", async (int count, IGameCatalog catalog) =>
    await catalog.GetTopRatedAsync(count));

app.MapGet("/games/stats", async (IGameCatalog catalog) =>
    await catalog.GetStatsByGenreAsync());

app.MapPost("/leaderboard/{pseudo}/points/{points:double}",
    async (string pseudo, double points, ILeaderboard lb) =>
    { await lb.AddPointsAsync(pseudo, points); return Results.Ok(); });

app.MapGet("/leaderboard/top/{count:int}",
    async (int count, ILeaderboard lb) => await lb.GetTopAsync(count));

app.MapGet("/leaderboard/rank/{pseudo}",
    async (string pseudo, ILeaderboard lb) => await lb.GetRankAsync(pseudo));

app.MapGet("/games/timed", async (IGameCatalog catalog) =>
{
    var chrono = System.Diagnostics.Stopwatch.StartNew();
    var jeux = await catalog.GetAllAsync();
    chrono.Stop();
    return new { nombre = jeux.Count, millisecondes = chrono.Elapsed.TotalMilliseconds };
});

// ---------- Séance 4 — Neo4j ----------
app.MapGet("/social/{pseudo}/friends",
    async (string pseudo, ISocialGraph g) => await g.GetFriendsAsync(pseudo));

app.MapGet("/social/{pseudo}/recommendations",
    async (string pseudo, int? depth, ISocialGraph g) =>
        await g.RecommendGamesAsync(pseudo, depth ?? 2));

app.MapGet("/social/{pseudo}/friend-suggestions",
    async (string pseudo, ISocialGraph g) => await g.SuggestFriendsAsync(pseudo));

// ---------- Séance 4 bis — la boutique (mission 2) ----------
app.MapPost("/shop/{pseudo}/buy/{titre}", async (string pseudo, string titre, ShopService shop) =>
{
    var r = await shop.BuyAsync(pseudo, titre);
    return r.Statut switch
    {
        AchatStatut.Ok => Results.Ok(r.Recu),
        AchatStatut.JeuInconnu => Results.NotFound(new { erreur = $"Jeu inconnu : {titre}" }),
        AchatStatut.JoueurInconnu => Results.NotFound(new { erreur = $"Joueur inconnu : {pseudo}" }),
        AchatStatut.SoldeInsuffisant => Results.Conflict(new { erreur = "Solde insuffisant" }),
        AchatStatut.DejaPossede => Results.Conflict(new { erreur = "Jeu déjà acheté" }),
        _ => Results.StatusCode(500)
    };
});

app.MapGet("/shop/bestsellers", async (RedisSales ventes) => await ventes.GetTopAsync(3));

// ---------- Séance 4 bis — la fiche joueur (mission 3) ----------
app.MapGet("/players/{pseudo}/profile", async (string pseudo, PlayerProfileService profils) =>
    await profils.GetAsync(pseudo) is { } profil ? Results.Ok(profil) : Results.NotFound());

// ---------- Séance 4 bis — reconstruire les projections depuis la source de vérité (mission 4) ----------
app.MapPost("/admin/resync", async (PixelHubContext db, ISocialGraph g, RedisSales ventes) =>
{
    var achats = await db.Purchases
        .Join(db.Players, a => a.PlayerId, p => p.Id, (a, p) => new { p.Pseudo, a.Titre, a.Date })
        .ToListAsync();

    foreach (var a in achats)                                      // MERGE : rejouable sans doublon
        await g.AddOwnershipAsync(a.Pseudo, a.Titre, a.Date.Year);

    await ventes.ReplaceAllAsync(achats
        .GroupBy(a => a.Titre)
        .Select(groupe => new GameSales(groupe.Key, groupe.Count())));

    return Results.Ok(new { achatsRejoues = achats.Count });
});

app.Run();