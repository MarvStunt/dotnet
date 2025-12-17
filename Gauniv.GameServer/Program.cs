using Gauniv.GameServer.Data;
using Gauniv.GameServer.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration de la base de données
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Port=5432;Database=gameserver;Username=admin;Password=password";

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(connectionString));

// Ajouter SignalR
builder.Services.AddSignalR();

// Ajouter CORS pour permettre les connexions depuis Godot
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin() // À restreindre en production
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Créer la base de données si elle n'existe pas
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    try
    {
        db.Database.EnsureCreated();
        Console.WriteLine("✅ Database connected successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Warning: Could not connect to database: {ex.Message}");
        Console.WriteLine("💡 Make sure PostgreSQL is running (docker-compose up postgres_primary)");
    }
}

app.UseCors();

// Servir les fichiers statiques (page de test)
app.UseStaticFiles();
app.UseDefaultFiles();

// Map SignalR Hub
app.MapHub<GameHub>("/gamehub");

app.MapGet("/", () => "Gauniv Game Server is running! Connect to /gamehub for SignalR");

Console.WriteLine("🎮 Gauniv Game Server started!");
Console.WriteLine("📡 SignalR Hub: ws://localhost:5000/gamehub");
Console.WriteLine("🔌 Waiting for Godot clients to connect...");

app.Run();
