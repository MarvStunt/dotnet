using Gauniv.GameServer.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Gauniv.GameServer.Hubs
{
    public class GameHub : Hub
    {
        private readonly GameDbContext _dbContext;

        public GameHub(GameDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"✅ Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"❌ Client disconnected: {Context.ConnectionId}");

            // Marquer le joueur comme déconnecté
            var player = await _dbContext.GamePlayers
                .FirstOrDefaultAsync(p => p.ConnectionId == Context.ConnectionId);

            if (player != null)
            {
                player.IsConnected = false;
                await _dbContext.SaveChangesAsync();

                await Clients.Group($"game_{player.GameSessionId}")
                    .SendAsync("PlayerDisconnected", player.PlayerName);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ==================== MJ (Game Master) Methods ====================

        /// <summary>
        /// Créer une nouvelle partie
        /// </summary>
        public async Task<string> CreateGame(string gameMasterName, int gridSize = 4)
        {
            Console.WriteLine($"TRYNG TO CREATE GAME FOR {gameMasterName} WITH GRID SIZE {gridSize}");
            var session = new GameSession
            {
                GameMasterName = gameMasterName,
                GridSize = gridSize,
                Status = GameSessionStatus.Waiting
            };

            _dbContext.GameSessions.Add(session);
            await _dbContext.SaveChangesAsync();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{session.Id}");

            Console.WriteLine($"🎮 Game created: {session.Code} by {gameMasterName}");
            return session.Code;
        }

        /// <summary>
        /// Démarrer la partie
        /// </summary>
        public async Task StartGame(string gameCode)
        {
            Console.WriteLine($"TRYNG TO START GAME {gameCode}");
            Console.WriteLine($"---------");

            var session = await _dbContext.GameSessions
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (session == null)
                throw new HubException("Game not found");

            if (session.Status != GameSessionStatus.Waiting)
                throw new HubException("Game already started");

            session.Status = GameSessionStatus.InProgress;
            session.StartedAt = DateTime.UtcNow;
            session.CurrentRound = 1;

            await _dbContext.SaveChangesAsync();

            await Clients.Group($"game_{session.Id}").SendAsync("GameStarted", session.Id);
            Console.WriteLine($"▶️ Game {gameCode} started");
        }

        /// <summary>
        /// Créer et diffuser le pattern du round actuel
        /// </summary>
        public async Task StartRound(string gameCode, string sequence)
        {
            var session = await _dbContext.GameSessions
                .Include(gs => gs.Rounds)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            Console.WriteLine($"TRYNG TO START ROUND FOR GAME {gameCode} WITH SEQUENCE {sequence}");
            Console.WriteLine($"SESSION IS NULL: {session == null}");
            Console.WriteLine($"SESSION STATUS: {session?.Status}");
            Console.WriteLine($"SESSION ROUNDS COUNT: {session?.Id}");
            Console.WriteLine($"---------");

            if (session == null)
                throw new HubException("Game not found");

            if (session.Status != GameSessionStatus.InProgress)
                throw new HubException("Game not in progress");

            var round = new GameRound
            {
                GameSessionId = session.Id,
                RoundNumber = session.CurrentRound,
                Pattern = sequence
            };

            _dbContext.GameRounds.Add(round);
            await _dbContext.SaveChangesAsync();

            // Parser la séquence JSON en int[]
            var pattern = JsonSerializer.Deserialize<int[]>(sequence) ?? Array.Empty<int>();

            // Diffuser le pattern à tous les joueurs
            await Clients.Group($"game_{session.Id}")
                .SendAsync("ShowPattern", pattern, session.CurrentRound);

            Console.WriteLine($"🔢 Round {session.CurrentRound} started - Pattern: {string.Join(", ", pattern)}");
        }

        /// <summary>
        /// Passer au round suivant
        /// </summary>
        public async Task NextRound(string gameCode)
        {
            var session = await _dbContext.GameSessions
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (session == null)
                throw new HubException("Game not found");

            session.CurrentRound++;
            await _dbContext.SaveChangesAsync();

            await Clients.Group($"game_{session.Id}")
                .SendAsync("RoundChanged", session.CurrentRound);

            // await StartRound(gameCode);
        }

        /// <summary>
        /// Arrêter la partie et afficher le classement final
        /// </summary>
        public async Task StopGame(string gameCode)
        {
            var session = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (session == null)
                throw new HubException("Game not found");

            session.Status = GameSessionStatus.Finished;
            session.FinishedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            var leaderboard = session.Players
                .OrderByDescending(p => p.Score)
                .Select(p => new { p.PlayerName, p.Score })
                .ToList();

            await Clients.Group($"game_{session.Id}")
                .SendAsync("GameEnded", leaderboard);

            Console.WriteLine($"🏁 Game {gameCode} ended");
        }

        // ==================== Player Methods ====================

        /// <summary>
        /// Rejoindre une partie
        /// </summary>
        public async Task<bool> JoinGame(string gameCode, string playerName)
        {
            Console.WriteLine($"TRYNG TO JOIN GAME {gameCode} AS {playerName}");
            var session = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (session == null)
                throw new HubException("Game not found");

            if (session.Status != GameSessionStatus.Waiting)
                throw new HubException("Game already started");

            // Vérifier si le joueur n'est pas déjà dans la partie
            // Log every player in the lobby
            Console.WriteLine("Current players in the lobby:");
            foreach (var p in session.Players)
            {
                Console.WriteLine($"- {p.PlayerName}");
            }

            if (session.Players.Any(p => p.PlayerName == playerName))
                throw new HubException("Player name already taken");

            var player = new GamePlayer
            {
                GameSessionId = session.Id,
                PlayerName = playerName,
                ConnectionId = Context.ConnectionId
            };

            _dbContext.GamePlayers.Add(player);
            await _dbContext.SaveChangesAsync();

            await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{session.Id}");

            await Clients.Group($"game_{session.Id}")
                .SendAsync("PlayerJoined", playerName, player.Id);

            Console.WriteLine($"👤 {playerName} joined game {gameCode}");
            Console.WriteLine("Current players in the lobby:");
            foreach (var p in session.Players)
            {
                Console.WriteLine($"- {p.PlayerName}");
            }

            return true;
        }

        /// <summary>
        /// Soumettre la tentative du joueur
        /// </summary>
        public async Task SubmitAttempt(string gameCode, List<int> attempt, long reactionTimeMs)
        {
            var session = await _dbContext.GameSessions
                .Include(gs => gs.Rounds)
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (session == null)
                throw new HubException("Game not found");

            if (session.Status != GameSessionStatus.InProgress)
                throw new HubException("Game not in progress");

            var player = session.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null)
                throw new HubException("You are not in this game");

            var currentRound = session.Rounds
                .FirstOrDefault(r => r.RoundNumber == session.CurrentRound);

            if (currentRound == null)
                throw new HubException("Current round not found");

            // Vérifier la réponse
            var pattern = JsonSerializer.Deserialize<List<int>>(currentRound.Pattern) ?? new List<int>();
            var isCorrect = pattern.SequenceEqual(attempt);

            // Calculer les points
            var pointsEarned = 0;
            if (isCorrect)
            {
                pointsEarned = 100 * session.CurrentRound;

                // Bonus de rapidité
                if (reactionTimeMs < 5000)
                {
                    pointsEarned += (int)(50 * (1 - reactionTimeMs / 5000.0));
                }

                player.Score += pointsEarned;
            }

            var playerAttempt = new PlayerAttempt
            {
                GameRoundId = currentRound.Id,
                GamePlayerId = player.Id,
                Attempt = JsonSerializer.Serialize(attempt),
                IsCorrect = isCorrect,
                PointsEarned = pointsEarned,
                ReactionTimeMs = reactionTimeMs
            };

            _dbContext.PlayerAttempts.Add(playerAttempt);
            await _dbContext.SaveChangesAsync();

            // Notifier tous les joueurs
            await Clients.Group($"game_{session.Id}")
                .SendAsync("PlayerSubmitted", player.PlayerName, isCorrect, pointsEarned, player.Score);

            Console.WriteLine($"📝 {player.PlayerName}: {(isCorrect ? "✓" : "✗")} (+{pointsEarned}pts, total: {player.Score})");
        }

        /// <summary>
        /// Obtenir le classement actuel
        /// </summary>
        public async Task<object> GetLeaderboard(string gameCode)
        {
            var session = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (session == null)
                throw new HubException("Game not found");

            var leaderboard = session.Players
                .OrderByDescending(p => p.Score)
                .Select(p => new
                {
                    p.PlayerName,
                    p.Score,
                    p.IsConnected
                })
                .ToList();

            return leaderboard;
        }
    }
}
