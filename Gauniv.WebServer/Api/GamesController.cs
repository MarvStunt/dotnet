#region Licence
// Cyril Tisserand
// Projet Gauniv - WebServer
// Gauniv 2025
//
// Licence MIT
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the “Software”), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// Any new method must be in a different namespace than the previous ones
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// The Software is provided “as is”, without warranty of any kind, express or implied,
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
using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Memory;
using Gauniv.WebServer.Data;
using Gauniv.WebServer.Dtos;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Gauniv.WebServer.Api
{
    [Route("api/1.0.0/[controller]")]
    [ApiController]
    public class GamesController(
        ApplicationDbContext appDbContext,
        IMapper mapper,
        UserManager<User> userManager,
        MappingProfile mp
    ) : ControllerBase
    {
        private readonly ApplicationDbContext appDbContext = appDbContext;
        private readonly IMapper mapper = mapper;
        private readonly UserManager<User> userManager = userManager;
        private readonly MappingProfile mp = mp;

        [HttpGet("debug")]
        public async Task<IActionResult> DebugData()
        {
            var gamesCount = await appDbContext.Games.CountAsync();
            var categoriesCount = await appDbContext.Categories.CountAsync();
            var usersCount = await appDbContext.Users.CountAsync();
            var userGamesCount = await appDbContext.UserGames.CountAsync();
            var gameCategoriesCount = await appDbContext.GameCategories.CountAsync();

            // Récupérer les utilisateurs avec leurs rôles
            var allUsers = await appDbContext.Users.ToListAsync();
            var usersWithRoles = new List<object>();

            foreach (var user in allUsers)
            {
                var roles = await userManager.GetRolesAsync(user);
                usersWithRoles.Add(
                    new
                    {
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        Roles = roles,
                    }
                );
            }

            return Ok(
                new
                {
                    Games = gamesCount,
                    Categories = categoriesCount,
                    Users = usersCount,
                    PurchasedGames = userGamesCount,
                    GameCategoryAssociations = gameCategoriesCount,
                    AllUsers = usersWithRoles,
                    AllGames = await appDbContext
                        .Games.Include(g => g.GameCategories)
                            .ThenInclude(gc => gc.Category)
                        .Select(g => new
                        {
                            g.Id,
                            g.Name,
                            g.Price,
                            Categories = g.GameCategories.Select(gc => gc.Category.Name),
                        })
                        .ToListAsync(),
                }
            );
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllGames()
        {
            var games = await appDbContext
                .Games.Include(g => g.GameCategories)
                    .ThenInclude(gc => gc.Category)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Description,
                    g.Price,
                    PayloadSize = g.Payload.Length,
                    Categories = g.GameCategories.Select(gc => new
                    {
                        gc.Category.Id,
                        gc.Category.Name,
                    }),
                })
                .ToListAsync();

            return Ok(games);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGame(int id)
        {
            var game = await appDbContext
                .Games.Include(g => g.GameCategories)
                    .ThenInclude(gc => gc.Category)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return NotFound(new { message = "Jeu non trouvé" });
            }

            return Ok(
                new
                {
                    game.Id,
                    game.Name,
                    game.Description,
                    game.Price,
                    PayloadSize = game.Payload.Length,
                    Categories = game.GameCategories.Select(gc => new
                    {
                        gc.Category.Id,
                        gc.Category.Name,
                    }),
                }
            );
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [IgnoreAntiforgeryToken]
        [RequestSizeLimit(524288000)] // 500 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
        public async Task<IActionResult> CreateGame(
            [FromForm] string name,
            [FromForm] string description,
            [FromForm] decimal price,
            [FromForm] IFormFile payload,
            [FromForm] string? categoryIds
        )
        {
            if (payload == null || payload.Length == 0)
            {
                return BadRequest(new { message = "Le fichier payload est requis" });
            }

            byte[] payloadBytes;
            using (var memoryStream = new MemoryStream())
            {
                await payload.CopyToAsync(memoryStream);
                payloadBytes = memoryStream.ToArray();
            }

            var game = new Game
            {
                Name = name,
                Description = description,
                Price = price,
                Payload = payloadBytes,
            };

            appDbContext.Games.Add(game);
            await appDbContext.SaveChangesAsync();
            if (!string.IsNullOrEmpty(categoryIds))
            {
                var categoryIdList = categoryIds
                    .Split(',')
                    .Select(id => int.TryParse(id.Trim(), out var result) ? result : 0)
                    .Where(id => id > 0)
                    .ToList();

                foreach (var categoryId in categoryIdList)
                {
                    var category = await appDbContext.Categories.FindAsync(categoryId);
                    if (category != null)
                    {
                        appDbContext.GameCategories.Add(
                            new GameCategory { GameId = game.Id, CategoryId = categoryId }
                        );
                    }
                }
                await appDbContext.SaveChangesAsync();
            }

            return CreatedAtAction(
                nameof(GetGame),
                new { id = game.Id },
                new
                {
                    game.Id,
                    game.Name,
                    game.Description,
                    game.Price,
                    message = "Jeu créé avec succès",
                }
            );
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateGame(
            int id,
            [FromForm] string? name,
            [FromForm] string? description,
            [FromForm] decimal? price,
            [FromForm] IFormFile? payload,
            [FromForm] string? categoryIds
        )
        {
            var game = await appDbContext
                .Games.Include(g => g.GameCategories)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
            {
                return NotFound(new { message = "Jeu non trouvé" });
            }

            if (!string.IsNullOrEmpty(name))
                game.Name = name;

            if (!string.IsNullOrEmpty(description))
                game.Description = description;

            if (price.HasValue)
                game.Price = price.Value;

            if (payload != null && payload.Length > 0)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await payload.CopyToAsync(memoryStream);
                    game.Payload = memoryStream.ToArray();
                }
            }

            if (categoryIds != null)
            {
                appDbContext.GameCategories.RemoveRange(game.GameCategories);

                if (!string.IsNullOrEmpty(categoryIds))
                {
                    var categoryIdList = categoryIds
                        .Split(',')
                        .Select(id => int.TryParse(id.Trim(), out var result) ? result : 0)
                        .Where(id => id > 0)
                        .ToList();

                    foreach (var categoryId in categoryIdList)
                    {
                        var category = await appDbContext.Categories.FindAsync(categoryId);
                        if (category != null)
                        {
                            appDbContext.GameCategories.Add(
                                new GameCategory { GameId = game.Id, CategoryId = categoryId }
                            );
                        }
                    }
                }
            }

            await appDbContext.SaveChangesAsync();
            return Ok(
                new
                {
                    game.Id,
                    game.Name,
                    game.Description,
                    game.Price,
                    message = "Jeu mis à jour avec succès",
                }
            );
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteGame(int id)
        {
            var game = await appDbContext.Games.FindAsync(id);
            if (game == null)
            {
                return NotFound(new { message = "Jeu non trouvé" });
            }

            appDbContext.Games.Remove(game);
            await appDbContext.SaveChangesAsync();
            return Ok(new { message = $"Jeu '{game.Name}' supprimé avec succès" });
        }

        [HttpGet("{id}/download")]
        [Authorize]
        public async Task<IActionResult> DownloadGame(int id)
        {
            var game = await appDbContext.Games.FindAsync(id);
            if (game == null)
            {
                return NotFound(new { message = "Jeu non trouvé" });
            }

            var user = await userManager.GetUserAsync(User);
            var isAdmin = await userManager.IsInRoleAsync(user!, "Admin");
            var hasGame = await appDbContext.UserGames.AnyAsync(ug =>
                ug.UserId == user!.Id && ug.GameId == id
            );

            if (!isAdmin && !hasGame)
            {
                return Forbid();
            }

            // Special handling for Memory Game - serve from filesystem
            if (game.Name == "Memory Game")
            {
                var gameDirectory = "/app/games/memorygame";
                
                // Check if running on Windows (development) vs Linux (Docker)
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    // Development path
                    gameDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "executable");
                }

                if (!Directory.Exists(gameDirectory))
                {
                    return NotFound(new { message = "Les fichiers du jeu sont introuvables" });
                }

                // Create a zip file in memory
                var memoryStream = new MemoryStream();
                try
                {
                    using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                    {
                        var files = Directory.GetFiles(gameDirectory, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var relativePath = Path.GetRelativePath(gameDirectory, file);
                            var entry = archive.CreateEntry(relativePath);
                            using var entryStream = entry.Open();
                            using var fileStream = System.IO.File.OpenRead(file);
                            await fileStream.CopyToAsync(entryStream);
                        }
                    }
                    
                    memoryStream.Position = 0;
                    return File(memoryStream, "application/zip", $"{game.Name}.zip");
                }
                catch
                {
                    memoryStream?.Dispose();
                    throw;
                }
            }

            // Default behavior for other games - serve from database payload
            return File(game.Payload, "application/octet-stream", $"{game.Name}.bin");
        }

        [HttpPost("{id}/purchase")]
        [HttpPost("PurchaseGame")] // Legacy route for backward compatibility
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PurchaseGame([FromRoute(Name = "id")] int? routeId = null, [FromQuery(Name = "id")] int? queryId = null)
        {
            // Support both route parameter and query string parameter for backward compatibility
            int id = routeId ?? queryId ?? 0;
            
            if (id == 0)
            {
                return BadRequest(new { message = "ID du jeu manquant" });
            }
            
            var game = await appDbContext.Games.FindAsync(id);
            if (game == null)
            {
                return NotFound(new { message = "Jeu non trouvé" });
            }

            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { message = "Utilisateur non authentifié" });
            }

            var alreadyOwned = await appDbContext.UserGames.AnyAsync(ug =>
                ug.UserId == user.Id && ug.GameId == id
            );

            if (alreadyOwned)
            {
                return BadRequest(new { message = "Vous possédez déjà ce jeu" });
            }

            var purchase = new UserGame
            {
                UserId = user.Id,
                GameId = id,
                PurchaseDate = DateTime.UtcNow,
            };

            appDbContext.UserGames.Add(purchase);
            await appDbContext.SaveChangesAsync();
            return Ok(
                new
                {
                    message = $"Jeu '{game.Name}' acheté avec succès !",
                    game = new
                    {
                        game.Id,
                        game.Name,
                        game.Description,
                        game.Price,
                    },
                    purchaseDate = purchase.PurchaseDate,
                }
            );
        }

        [HttpGet("my-games")]
        [Authorize]
        public async Task<IActionResult> GetMyGames()
        {
            var user = await userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new { message = "Utilisateur non authentifié" });
            }

            var myGames = await appDbContext
                .UserGames.Where(ug => ug.UserId == user.Id)
                .Include(ug => ug.Game)
                    .ThenInclude(g => g.GameCategories)
                        .ThenInclude(gc => gc.Category)
                .Select(ug => new
                {
                    ug.Game.Id,
                    ug.Game.Name,
                    ug.Game.Description,
                    ug.Game.Price,
                    PayloadSize = ug.Game.Payload.Length,
                    PurchaseDate = ug.PurchaseDate,
                    Categories = ug.Game.GameCategories.Select(gc => new
                    {
                        gc.Category.Id,
                        gc.Category.Name,
                    }),
                })
                .ToListAsync();

            return Ok(new { totalGames = myGames.Count, games = myGames });
        }

        [HttpGet("{id}/owned")]
        [Authorize]
        public async Task<IActionResult> CheckIfOwned(int id)
        {
            var user = await userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new { message = "Utilisateur non authentifié" });
            }

            var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
            var owned = await appDbContext.UserGames.AnyAsync(ug =>
                ug.UserId == user.Id && ug.GameId == id
            );

            return Ok(
                new
                {
                    gameId = id,
                    owned = owned,
                    isAdmin = isAdmin,
                    canDownload = owned || isAdmin,
                }
            );
        }
    }
}
