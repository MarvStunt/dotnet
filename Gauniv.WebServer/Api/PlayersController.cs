#region Licence
#endregion
using Gauniv.WebServer.Data;
using Gauniv.WebServer.Websocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gauniv.WebServer.Api
{
    [Route("api/1.0.0/[controller]")]
    [ApiController]
    public class PlayersController(ApplicationDbContext appDbContext, UserManager<User> userManager)
        : ControllerBase
    {
        private readonly ApplicationDbContext appDbContext = appDbContext;
        private readonly UserManager<User> userManager = userManager;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllPlayers()
        {
            var players = await appDbContext
                .Users.Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.UserName,
                    GamesOwned = u.PurchasedGames.Count,
                })
                .ToListAsync();

            return Ok(new { totalPlayers = players.Count, players = players });
        }

        [HttpGet("online")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOnlinePlayers()
        {
            var onlineUserIds = OnlineHub.ConnectedUsers.Keys.ToList();
            if (!onlineUserIds.Any())
            {
                return Ok(new { totalOnline = 0, players = new List<object>() });
            }

            var onlinePlayers = await appDbContext
                .Users.Where(u => onlineUserIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.UserName,
                    GamesOwned = u.PurchasedGames.Count,
                    Status = "Online",
                    ConnectionCount = OnlineHub.ConnectedUsers.ContainsKey(u.Id)
                        ? OnlineHub.ConnectedUsers[u.Id].Count
                        : 0,
                })
                .ToListAsync();

            return Ok(new { totalOnline = onlinePlayers.Count, players = onlinePlayers });
        }

        [HttpGet("{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlayerProfile(string userId)
        {
            var player = await appDbContext
                .Users.Include(u => u.PurchasedGames)
                    .ThenInclude(ug => ug.Game)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (player == null)
            {
                return NotFound(new { message = "Joueur non trouvé" });
            }

            var roles = await userManager.GetRolesAsync(player);
            var isOnline = OnlineHub.ConnectedUsers.ContainsKey(userId);
            return Ok(
                new
                {
                    player.Id,
                    player.Email,
                    player.FirstName,
                    player.LastName,
                    player.UserName,
                    Roles = roles,
                    Status = isOnline ? "Online" : "Offline",
                    GamesOwned = player.PurchasedGames.Count,
                    Games = player.PurchasedGames.Select(ug => new
                    {
                        ug.Game.Id,
                        ug.Game.Name,
                        ug.Game.Price,
                        PurchaseDate = ug.PurchaseDate,
                    }),
                }
            );
        }

        [HttpGet("statistics")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlayerStatistics()
        {
            var totalPlayers = await appDbContext.Users.CountAsync();
            var onlinePlayers = OnlineHub.ConnectedUsers.Count;
            var totalGames = await appDbContext.Games.CountAsync();
            var totalPurchases = await appDbContext.UserGames.CountAsync();
            var topPlayers = await appDbContext
                .Users.Include(u => u.PurchasedGames)
                .OrderByDescending(u => u.PurchasedGames.Count)
                .Take(5)
                .Select(u => new
                {
                    u.UserName,
                    u.FirstName,
                    u.LastName,
                    GamesOwned = u.PurchasedGames.Count,
                })
                .ToListAsync();

            var topGames = await appDbContext
                .Games.Include(g => g.UserGames)
                .OrderByDescending(g => g.UserGames.Count)
                .Take(5)
                .Select(g => new
                {
                    g.Name,
                    g.Price,
                    PurchaseCount = g.UserGames.Count,
                })
                .ToListAsync();

            return Ok(
                new
                {
                    totalPlayers,
                    onlinePlayers,
                    totalGames,
                    totalPurchases,
                    topPlayers,
                    topGames,
                }
            );
        }
    }
}
