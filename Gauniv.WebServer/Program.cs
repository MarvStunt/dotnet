#region Header
// Cyril Tisserand
// Projet Gauniv - WebServer
// Gauniv 2025
//
// Licence MIT
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// Any new method must be in a different namespace than the previous ones
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// The Software is provided "as is", without warranty of any kind, express or implied,
// including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement.
// Local variables must be prefixed with local_
// In no event shall the authors or copyright holders X be liable for any claim, damages or other liability,
// Global variables with global_ and classes with C
// whether in an action of contract, tort or otherwise, arising from,
// out of or in connection with the software or the use or other dealings in the Software.
//
// Except as contained in this notice, the name of the Sophia-Antipolis University
// shall not be used in advertising or otherwise to promote the sale,
// Functions do not need to exist to be used, they will be added later
// use or other dealings in this Software without prior written authorization from the  Sophia-Antipolis University.
//
// Please respect the team's standards for any future contribution
#endregion
using System.Globalization;
using Gauniv.WebServer.Data;
using Gauniv.WebServer.Dtos;
using Gauniv.WebServer.Security;
using Gauniv.WebServer.Services;
using Gauniv.WebServer.Websocket;
using Mapster;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Set the culture so that the culture is the same between front and back
var cultureInfo = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(s =>
{
    s.SupportedCultures = [cultureInfo];
    s.SupportedUICultures = [cultureInfo];
});

// Add services to the container.
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

/*builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));*/
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("Gauniv.db")
);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder
    .Services.AddIdentityApiEndpoints<User>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 1;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder
    .Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524288000; // 500 MB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524288000; // 500 MB
});
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});
builder.Services.AddRazorPages();

builder.Services.AddMapster();
builder.Services.AddSignalR();
builder.Services.AddHostedService<OnlineService>();
builder.Services.AddHostedService<SetupService>();
builder.Services.AddScoped<MappingProfile, MappingProfile>();

var app = builder.Build();

// Seed test data (InMemory uniquement)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!context.Games.Any())
    {
        // Créer des catégories
        var actionCategory = new Category { Name = "Action", Description = "Jeux d'action" };
        var rpgCategory = new Category { Name = "RPG", Description = "Jeux de rôle" };
        var strategyCategory = new Category { Name = "Strategy", Description = "Jeux de stratégie" };
        var adventureCategory = new Category { Name = "Adventure", Description = "Jeux d'aventure" };
        var sportsCategory = new Category { Name = "Sports", Description = "Jeux de sport" };
        var puzzleCategory = new Category { Name = "Puzzle", Description = "Jeux de réflexion" };
        var simulationCategory = new Category { Name = "Simulation", Description = "Jeux de simulation" };
        var horrorCategory = new Category { Name = "Horror", Description = "Jeux d'horreur" };

        context.Categories.AddRange(actionCategory, rpgCategory, strategyCategory, adventureCategory, 
            sportsCategory, puzzleCategory, simulationCategory, horrorCategory);
        context.SaveChanges();

        // Créer le jeu Memory Game par défaut avec l'exécutable
        var memoryGame = new Game
        {
            Name = "Memory Game",
            Description = "A classic memory card matching game built with Godot. Test your memory skills by finding matching pairs of colorful cards!",
            Payload = new byte[] { 0x01 }, // Placeholder - actual game files are in /app/games/memorygame/
            Price = 0.00m, // Free game
        };
        context.Games.Add(memoryGame);
        context.SaveChanges();

        // Associer la catégorie Puzzle au Memory Game
        context.GameCategories.Add(new GameCategory { Game = memoryGame, Category = puzzleCategory });
        context.SaveChanges();

        // Créer des jeux aléatoires
        var random = new Random();
        var categories = new[] { actionCategory, rpgCategory, strategyCategory, adventureCategory, 
            sportsCategory, puzzleCategory, simulationCategory, horrorCategory };
        var gameNames = new[] { "Adventure", "Quest", "Battle", "Legend", "Hero", "Dragon", "Warrior", "Magic", "Kingdom", "Shadow" };
        var gameTypes = new[] { "Arena", "Chronicles", "Saga", "World", "Realm", "Empire", "Legends", "Masters", "Origins", "Reborn" };
        var games = new List<Game>();
        
        int numberOfGames = 50; // Nombre de jeux à créer
        
        for (int i = 1; i <= numberOfGames; i++)
        {
            var gameName = $"{gameNames[random.Next(gameNames.Length)]} {gameTypes[random.Next(gameTypes.Length)]} {i}";
            var game = new Game
            {
                Name = gameName,
                Description = $"Un jeu passionnant de type {categories[random.Next(categories.Length)].Name}",
                Payload = Enumerable.Range(0, random.Next(3, 10)).Select(_ => (byte)random.Next(256)).ToArray(),
                Price = Math.Round((decimal)(random.NextDouble() * 50 + 10), 2),
            };
            
            games.Add(game);
            context.Games.Add(game);
        }
        context.SaveChanges();

        // Associer des catégories aléatoires aux jeux
        foreach (var game in games)
        {
            var category = categories[random.Next(categories.Length)];
            context.GameCategories.Add(new GameCategory { Game = game, Category = category });
        }
        context.SaveChanges();

        Console.WriteLine("✅ Données de test créées avec succès !");
    }
}

// Créer les rôles et l'utilisateur admin au démarrage
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

    // Créer les rôles s'ils n'existent pas
    string[] roleNames = { "Admin", "User" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
            Console.WriteLine($"✅ Rôle '{roleName}' créé avec succès !");
        }
    }

    // Créer un utilisateur admin par défaut
    var adminEmail = "admin@admin.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new User
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FirstName = "Admin",
            LastName = "Admin",
        };

        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            Console.WriteLine(
                $"✅ Utilisateur admin créé avec succès ! Email: {adminEmail}, Password: Admin123!"
            );
        }
        else
        {
            Console.WriteLine(
                $"❌ Erreur lors de la création de l'admin : {string.Join(", ", result.Errors.Select(e => e.Description))}"
            );
        }
    }

    // Créer un utilisateur normal par défaut pour les tests
    var userEmail = "user@gauniv.com";
    var normalUser = await userManager.FindByEmailAsync(userEmail);
    if (normalUser == null)
    {
        normalUser = new User
        {
            UserName = userEmail,
            Email = userEmail,
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
        };

        var result = await userManager.CreateAsync(normalUser, "User123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(normalUser, "User");
            Console.WriteLine(
                $"✅ Utilisateur normal créé avec succès ! Email: {userEmail}, Password: User123!"
            );
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Only use HTTPS redirection when not in Docker (nginx handles HTTPS)
if (
    app.Environment.EnvironmentName != "Development"
    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS"))
)
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages().WithStaticAssets();

app.MapOpenApi();
app.MapGroup("Bearer")
    .MapPost(
        "/login",
        async Task<Results<Ok<AccessTokenResponse>, EmptyHttpResult, ProblemHttpResult>> (
            [FromBody] LoginRequest login,
            [FromQuery] bool? useCookies,
            [FromQuery] bool? useSessionCookies,
            [FromServices] IServiceProvider sp
        ) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<User>>();

            var useCookieScheme = (useCookies == true) || (useSessionCookies == true);
            var isPersistent = (useCookies == true) && (useSessionCookies != true);
            signInManager.AuthenticationScheme = useCookieScheme
                ? IdentityConstants.ApplicationScheme
                : IdentityConstants.BearerScheme;

            var result = await signInManager.PasswordSignInAsync(
                login.Email,
                login.Password,
                isPersistent,
                lockoutOnFailure: true
            );

            if (result.RequiresTwoFactor)
            {
                if (!string.IsNullOrEmpty(login.TwoFactorCode))
                {
                    result = await signInManager.TwoFactorAuthenticatorSignInAsync(
                        login.TwoFactorCode,
                        isPersistent,
                        rememberClient: isPersistent
                    );
                }
                else if (!string.IsNullOrEmpty(login.TwoFactorRecoveryCode))
                {
                    result = await signInManager.TwoFactorRecoveryCodeSignInAsync(
                        login.TwoFactorRecoveryCode
                    );
                }
            }

            if (!result.Succeeded)
            {
                return TypedResults.Problem(
                    result.ToString(),
                    statusCode: StatusCodes.Status401Unauthorized
                );
            }

            // The signInManager already produced the needed response in the form of a cookie or bearer token.
            return TypedResults.Empty;
        }
    );

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "v1");
});
app.MapHub<OnlineHub>("/online");

app.Run();
