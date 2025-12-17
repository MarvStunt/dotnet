#region Header
#endregion
using Gauniv.WebServer.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gauniv.WebServer.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameSessionController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public GameSessionController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Obtenir toutes les parties en attente
        /// </summary>
        [HttpGet("waiting")]
        public async Task<IActionResult> GetWaitingGames()
        {
            var local_games = await _dbContext.GameSessions
                .Include(gs => gs.GameMaster)
                .Include(gs => gs.Players)
                .Where(gs => gs.Status == GameSessionStatus.Waiting)
                .Select(gs => new
                {
                    gs.Id,
                    gs.Code,
                    GameMaster = gs.GameMaster!.UserName,
                    PlayerCount = gs.Players.Count,
                    gs.GridSize,
                    gs.CreatedAt
                })
                .ToListAsync();

            return Ok(local_games);
        }

        /// <summary>
        /// Obtenir les détails d'une partie
        /// </summary>
        [HttpGet("{code}")]
        public async Task<IActionResult> GetGameDetails(string code)
        {
            var local_game = await _dbContext.GameSessions
                .Include(gs => gs.GameMaster)
                .Include(gs => gs.Players)
                    .ThenInclude(p => p.User)
                .Include(gs => gs.Rounds)
                .FirstOrDefaultAsync(gs => gs.Code == code);

            if (local_game == null)
                return NotFound("Game not found");

            return Ok(new
            {
                local_game.Id,
                local_game.Code,
                GameMaster = local_game.GameMaster!.UserName,
                local_game.Status,
                local_game.CurrentRound,
                local_game.GridSize,
                local_game.CreatedAt,
                local_game.StartedAt,
                local_game.FinishedAt,
                Players = local_game.Players.Select(p => new
                {
                    p.Id,
                    UserName = p.User!.UserName,
                    p.Score,
                    p.IsConnected,
                    p.JoinedAt
                }),
                TotalRounds = local_game.Rounds.Count
            });
        }

        /// <summary>
        /// Obtenir le classement d'une partie
        /// </summary>
        [HttpGet("{code}/leaderboard")]
        public async Task<IActionResult> GetLeaderboard(string code)
        {
            var local_game = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(gs => gs.Code == code);

            if (local_game == null)
                return NotFound("Game not found");

            var local_leaderboard = local_game.Players
                .OrderByDescending(p => p.Score)
                .Select((p, index) => new
                {
                    Rank = index + 1,
                    UserName = p.User!.UserName,
                    p.Score,
                    p.IsConnected
                })
                .ToList();

            return Ok(local_leaderboard);
        }

        /// <summary>
        /// Obtenir les parties actives d'un utilisateur
        /// </summary>
        [HttpGet("my-games")]
        [Authorize]
        public async Task<IActionResult> GetMyGames()
        {
            var local_userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (local_userId == null)
                return Unauthorized();

            var local_games = await _dbContext.GamePlayers
                .Include(gp => gp.GameSession)
                    .ThenInclude(gs => gs!.GameMaster)
                .Where(gp => gp.UserId == local_userId)
                .Select(gp => new
                {
                    gp.GameSession!.Code,
                    gp.GameSession.Status,
                    gp.GameSession.CurrentRound,
                    GameMaster = gp.GameSession.GameMaster!.UserName,
                    gp.Score,
                    gp.GameSession.CreatedAt
                })
                .ToListAsync();

            return Ok(local_games);
        }

        /// <summary>
        /// Obtenir l'historique des rounds d'une partie
        /// </summary>
        [HttpGet("{code}/rounds")]
        public async Task<IActionResult> GetRounds(string code)
        {
            var local_game = await _dbContext.GameSessions
                .Include(gs => gs.Rounds)
                    .ThenInclude(r => r.PlayerAttempts)
                        .ThenInclude(pa => pa.GamePlayer)
                            .ThenInclude(gp => gp!.User)
                .FirstOrDefaultAsync(gs => gs.Code == code);

            if (local_game == null)
                return NotFound("Game not found");

            var local_rounds = local_game.Rounds
                .OrderBy(r => r.RoundNumber)
                .Select(r => new
                {
                    r.RoundNumber,
                    r.Pattern,
                    r.CreatedAt,
                    Attempts = r.PlayerAttempts.Select(pa => new
                    {
                        UserName = pa.GamePlayer!.User!.UserName,
                        pa.IsCorrect,
                        pa.PointsEarned,
                        pa.ReactionTimeMs,
                        pa.SubmittedAt
                    })
                })
                .ToList();

            return Ok(local_rounds);
        }

        /// <summary>
        /// Supprimer une partie (MJ uniquement)
        /// </summary>
        [HttpDelete("{code}")]
        [Authorize]
        public async Task<IActionResult> DeleteGame(string code)
        {
            var local_userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (local_userId == null)
                return Unauthorized();

            var local_game = await _dbContext.GameSessions
                .FirstOrDefaultAsync(gs => gs.Code == code && gs.GameMasterId == local_userId);

            if (local_game == null)
                return NotFound("Game not found or you are not the game master");

            _dbContext.GameSessions.Remove(local_game);
            await _dbContext.SaveChangesAsync();

            return Ok("Game deleted");
        }

        /// <summary>
        /// Obtenir les statistiques globales
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var local_totalGames = await _dbContext.GameSessions.CountAsync();
            var local_activeGames = await _dbContext.GameSessions
                .CountAsync(gs => gs.Status == GameSessionStatus.InProgress);
            var local_totalPlayers = await _dbContext.GamePlayers.CountAsync();
            var local_averageScore = await _dbContext.GamePlayers.AverageAsync(gp => (double?)gp.Score) ?? 0;

            return Ok(new
            {
                TotalGames = local_totalGames,
                ActiveGames = local_activeGames,
                TotalPlayers = local_totalPlayers,
                AverageScore = Math.Round(local_averageScore, 2)
            });
        }
    }
}
