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

            var player = await _dbContext.GamePlayers
                .Include(p => p.GameSession)
                .FirstOrDefaultAsync(p => p.ConnectionId == Context.ConnectionId);

            if (player != null)
            {
                player.IsConnected = false;
                await _dbContext.SaveChangesAsync();

                string role = player.PlayerName == player.GameSession?.GameMasterName ? "master" : "player";

                if (role == "master")
                {
                    Console.WriteLine($"👑 Game Master {player.PlayerName} disconnected - ending game session");
                    
                    await Clients.Group($"game_{player.GameSessionId}")
                        .SendAsync("GameMasterDisconnected", player.PlayerName);
                    
                    if (player.GameSession != null)
                    {
                        player.GameSession.Status = GameSessionStatus.Finished;
                        player.GameSession.FinishedAt = DateTime.UtcNow;
                        await _dbContext.SaveChangesAsync();
                    }
                }
                else
                {
                    await Clients.Group($"game_{player.GameSessionId}")
                        .SendAsync("PlayerDisconnected", player.PlayerName, role);
                }
                
                Console.WriteLine($"👤 Player {player.PlayerName} ({role}) disconnected from game");
            }

            await base.OnDisconnectedAsync(exception);
        }


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

            var masterPlayer = new GamePlayer
            {
                GameSessionId = session.Id,
                PlayerName = gameMasterName,
                ConnectionId = Context.ConnectionId
            };

            _dbContext.GamePlayers.Add(masterPlayer);
            await _dbContext.SaveChangesAsync();

            await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{session.Id}");

            Console.WriteLine($"🎮 Game created: {session.Code} by {gameMasterName}");
            return session.Code;
        }

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

            var pattern = JsonSerializer.Deserialize<int[]>(sequence) ?? Array.Empty<int>();

            await Clients.Group($"game_{session.Id}")
                .SendAsync("ShowPattern", pattern, session.CurrentRound);

            Console.WriteLine($"🔢 Round {session.CurrentRound} started - Pattern: {string.Join(", ", pattern)}");
        }

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

        }

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


        public async Task<bool> JoinGame(string gameCode, string playerName)
        {
            Console.WriteLine($"TRYNG TO JOIN GAME {gameCode} AS {playerName}");
            var session = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (session == null)
                throw new HubException("Game not found");

            Console.WriteLine("Current players in the lobby:");
            foreach (var p in session.Players)
            {
                Console.WriteLine($"- {p.PlayerName} (Connected: {p.IsConnected})");
            }

            var existingPlayer = session.Players.FirstOrDefault(p => p.PlayerName == playerName);
            
            if (existingPlayer != null)
            {
                if (!existingPlayer.IsConnected)
                {
                    Console.WriteLine($"🔄 Player {playerName} is reconnecting...");
                    existingPlayer.ConnectionId = Context.ConnectionId;
                    existingPlayer.IsConnected = true;
                    await _dbContext.SaveChangesAsync();
                    
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{session.Id}");
                    
                    await Clients.Group($"game_{session.Id}")
                        .SendAsync("PlayerReconnected", playerName, existingPlayer.PlayerName == session.GameMasterName ? "master" : "player");
                    
                    Console.WriteLine($"✅ {playerName} reconnected successfully");
                }
                else
                {
                    throw new HubException("Player name already taken");
                }
            }
            else
            {
                if (session.Status != GameSessionStatus.Waiting)
                    throw new HubException("Game already started");
                
                var player = new GamePlayer
                {
                    GameSessionId = session.Id,
                    PlayerName = playerName,
                    ConnectionId = Context.ConnectionId
                };

                _dbContext.GamePlayers.Add(player);
                await _dbContext.SaveChangesAsync();

                await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{session.Id}");
                
                // Notify all players about the new player
                await Clients.Group($"game_{session.Id}")
                    .SendAsync("PlayerJoined", playerName, player.Id);
            }

            // Send updated player list to all clients
            var playerList = session.Players
                .Select(p => new
                {
                    playerName = p.PlayerName,
                    role = p.PlayerName == session.GameMasterName ? "master" : "player",
                    score = p.Score,
                    isConnected = p.IsConnected
                })
                .ToList();

            await Clients.Group($"game_{session.Id}")
                .SendAsync("PlayerListReceived", playerList);

            Console.WriteLine($"👤 {playerName} joined game {gameCode}");
            Console.WriteLine("Current players in the lobby:");
            foreach (var p in session.Players)
            {
                Console.WriteLine($"- {p.PlayerName}");
            }

            return true;
        }

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

            var pattern = JsonSerializer.Deserialize<List<int>>(currentRound.Pattern) ?? new List<int>();
            var isCorrect = pattern.SequenceEqual(attempt);

            var pointsEarned = 0;
            if (isCorrect)
            {
                pointsEarned = 100 * session.CurrentRound;

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

            await Clients.Group($"game_{session.Id}")
                .SendAsync("PlayerSubmitted", player.PlayerName, isCorrect, pointsEarned, player.Score);

            Console.WriteLine($"📝 {player.PlayerName}: {(isCorrect ? "✓" : "✗")} (+{pointsEarned}pts, total: {player.Score})");
        }

        public async Task GetPlayerList(string gameCode)
        {
            var session = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (session == null)
                throw new HubException("Game not found");

            var playerList = session.Players
                .Select(p => new
                {
                    playerName = p.PlayerName,
                    role = p.PlayerName == session.GameMasterName ? "master" : "player",
                    score = p.Score,
                    isConnected = p.IsConnected
                })
                .ToList();

            await Clients.Caller.SendAsync("PlayerListReceived", playerList);
            
            Console.WriteLine($"📋 Sent player list to {Context.ConnectionId}: {playerList.Count} players");
        }

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
