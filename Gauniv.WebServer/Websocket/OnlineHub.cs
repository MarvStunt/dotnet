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
using Gauniv.WebServer.Data;
using Gauniv.WebServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public class OnlineStatus()
{
    public User User { get; set; }
    public int Count { get; set; }
}

namespace Gauniv.WebServer.Websocket
{
    public class OnlineHub : Hub
    {
        public static Dictionary<string, OnlineStatus> ConnectedUsers = [];
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _dbContext;

        public OnlineHub(UserManager<User> userManager, ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _dbContext = dbContext;
        }

        public async override Task OnConnectedAsync()
        {
            var local_userId = Context.UserIdentifier;
            if (local_userId != null)
            {
                var local_user = await _userManager.FindByIdAsync(local_userId);
                if (local_user != null)
                {
                    if (!ConnectedUsers.ContainsKey(local_userId))
                    {
                        ConnectedUsers[local_userId] = new OnlineStatus { User = local_user, Count = 0 };
                    }
                    ConnectedUsers[local_userId].Count++;
                    
                    // Notifier tous les clients qu'un utilisateur est en ligne
                    await Clients.All.SendAsync("UserOnline", local_user.UserName, local_userId);
                }
            }
            await base.OnConnectedAsync();
        }

        public async override Task OnDisconnectedAsync(Exception? exception)
        {
            var local_userId = Context.UserIdentifier;
            if (local_userId != null && ConnectedUsers.ContainsKey(local_userId))
            {
                ConnectedUsers[local_userId].Count--;
                if (ConnectedUsers[local_userId].Count <= 0)
                {
                    var local_user = ConnectedUsers[local_userId].User;
                    ConnectedUsers.Remove(local_userId);
                    
                    // Notifier tous les clients qu'un utilisateur est hors ligne
                    await Clients.All.SendAsync("UserOffline", local_user.UserName, local_userId);
                    
                    // Marquer le joueur comme déconnecté dans toutes ses parties actives
                    var local_activePlayers = await _dbContext.GamePlayers
                        .Where(gp => gp.UserId == local_userId && gp.IsConnected)
                        .ToListAsync();
                    
                    foreach (var local_player in local_activePlayers)
                    {
                        local_player.IsConnected = false;
                        await Clients.Group($"game_{local_player.GameSessionId}")
                            .SendAsync("PlayerDisconnected", local_player.Id, local_user.UserName);
                    }
                    await _dbContext.SaveChangesAsync();
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        // ========== Méthodes pour le MJ ==========

        /// <summary>
        /// Créer une nouvelle partie (MJ uniquement)
        /// </summary>
        public async Task<string> CreateGame(int gridSize = 4)
        {
            var local_userId = Context.UserIdentifier;
            if (local_userId == null)
                throw new HubException("User not authenticated");

            var local_gameSession = new GameSession
            {
                GameMasterId = local_userId,
                GridSize = gridSize,
                Status = GameSessionStatus.Waiting
            };

            _dbContext.GameSessions.Add(local_gameSession);
            await _dbContext.SaveChangesAsync();

            // Rejoindre le groupe SignalR
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{local_gameSession.Id}");

            return local_gameSession.Code;
        }

        /// <summary>
        /// Démarrer la partie (MJ uniquement)
        /// </summary>
        public async Task StartGame(string gameCode)
        {
            var local_userId = Context.UserIdentifier;
            var local_session = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode && gs.GameMasterId == local_userId);

            if (local_session == null)
                throw new HubException("Game not found or you are not the game master");

            if (local_session.Status != GameSessionStatus.Waiting)
                throw new HubException("Game already started");

            local_session.Status = GameSessionStatus.InProgress;
            local_session.StartedAt = DateTime.UtcNow;
            local_session.CurrentRound = 1;

            await _dbContext.SaveChangesAsync();

            // Notifier tous les joueurs
            await Clients.Group($"game_{local_session.Id}").SendAsync("GameStarted", local_session.Id);

            // Démarrer le premier round
            await StartRound(gameCode);
        }

        /// <summary>
        /// Créer un nouveau pattern pour le round actuel (MJ uniquement)
        /// </summary>
        public async Task StartRound(string gameCode)
        {
            var local_userId = Context.UserIdentifier;
            var local_session = await _dbContext.GameSessions
                .Include(gs => gs.Rounds)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode && gs.GameMasterId == local_userId);

            if (local_session == null)
                throw new HubException("Game not found or you are not the game master");

            if (local_session.Status != GameSessionStatus.InProgress)
                throw new HubException("Game not in progress");

            // Générer un pattern aléatoire (nombre de cases = numéro du round)
            var local_random = new Random();
            var local_maxCells = local_session.GridSize * local_session.GridSize;
            var local_pattern = new List<int>();
            
            for (int i = 0; i < local_session.CurrentRound; i++)
            {
                local_pattern.Add(local_random.Next(0, local_maxCells));
            }

            var local_round = new GameRound
            {
                GameSessionId = local_session.Id,
                RoundNumber = local_session.CurrentRound,
                Pattern = JsonSerializer.Serialize(local_pattern)
            };

            _dbContext.GameRounds.Add(local_round);
            await _dbContext.SaveChangesAsync();

            // Envoyer le pattern aux joueurs
            await Clients.Group($"game_{local_session.Id}").SendAsync("ShowPattern", local_pattern, local_session.CurrentRound);
        }

        /// <summary>
        /// Arrêter la partie (MJ uniquement)
        /// </summary>
        public async Task StopGame(string gameCode)
        {
            var local_userId = Context.UserIdentifier;
            var local_session = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode && gs.GameMasterId == local_userId);

            if (local_session == null)
                throw new HubException("Game not found or you are not the game master");

            local_session.Status = GameSessionStatus.Finished;
            local_session.FinishedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Envoyer les scores finaux
            var local_leaderboard = local_session.Players
                .OrderByDescending(p => p.Score)
                .Select(p => new { p.User!.UserName, p.Score })
                .ToList();

            await Clients.Group($"game_{local_session.Id}").SendAsync("GameEnded", local_leaderboard);
        }

        // ========== Méthodes pour les joueurs ==========

        /// <summary>
        /// Rejoindre une partie via un code
        /// </summary>
        public async Task<bool> JoinGame(string gameCode)
        {
            var local_userId = Context.UserIdentifier;
            if (local_userId == null)
                throw new HubException("User not authenticated");

            var local_session = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (local_session == null)
                throw new HubException("Game not found");

            if (local_session.Status != GameSessionStatus.Waiting)
                throw new HubException("Game already started");

            // Vérifier si le joueur n'est pas déjà dans la partie
            if (local_session.Players.Any(p => p.UserId == local_userId))
                throw new HubException("Already in this game");

            var local_player = new GamePlayer
            {
                GameSessionId = local_session.Id,
                UserId = local_userId,
                ConnectionId = Context.ConnectionId
            };

            _dbContext.GamePlayers.Add(local_player);
            await _dbContext.SaveChangesAsync();

            // Rejoindre le groupe SignalR
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game_{local_session.Id}");

            var local_user = await _userManager.FindByIdAsync(local_userId);
            
            // Notifier tous les joueurs de la partie
            await Clients.Group($"game_{local_session.Id}").SendAsync("PlayerJoined", local_user?.UserName, local_player.Id);

            return true;
        }

        /// <summary>
        /// Soumettre la tentative du joueur
        /// </summary>
        public async Task SubmitAttempt(string gameCode, List<int> attempt, long reactionTimeMs)
        {
            var local_userId = Context.UserIdentifier;
            if (local_userId == null)
                throw new HubException("User not authenticated");

            var local_session = await _dbContext.GameSessions
                .Include(gs => gs.Rounds)
                .Include(gs => gs.Players)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (local_session == null)
                throw new HubException("Game not found");

            if (local_session.Status != GameSessionStatus.InProgress)
                throw new HubException("Game not in progress");

            var local_player = local_session.Players.FirstOrDefault(p => p.UserId == local_userId);
            if (local_player == null)
                throw new HubException("You are not in this game");

            var local_currentRound = local_session.Rounds
                .FirstOrDefault(r => r.RoundNumber == local_session.CurrentRound);

            if (local_currentRound == null)
                throw new HubException("Current round not found");

            // Vérifier si la tentative correspond au pattern
            var local_pattern = JsonSerializer.Deserialize<List<int>>(local_currentRound.Pattern) ?? new List<int>();
            var local_isCorrect = local_pattern.SequenceEqual(attempt);

            // Calculer les points (bonus si rapide)
            var local_pointsEarned = 0;
            if (local_isCorrect)
            {
                local_pointsEarned = 100 * local_session.CurrentRound;
                
                // Bonus de rapidité (max 50 points)
                if (reactionTimeMs < 5000)
                {
                    local_pointsEarned += (int)(50 * (1 - reactionTimeMs / 5000.0));
                }
                
                local_player.Score += local_pointsEarned;
            }

            var local_playerAttempt = new PlayerAttempt
            {
                GameRoundId = local_currentRound.Id,
                GamePlayerId = local_player.Id,
                Attempt = JsonSerializer.Serialize(attempt),
                IsCorrect = local_isCorrect,
                PointsEarned = local_pointsEarned,
                ReactionTimeMs = reactionTimeMs
            };

            _dbContext.PlayerAttempts.Add(local_playerAttempt);
            await _dbContext.SaveChangesAsync();

            var local_user = await _userManager.FindByIdAsync(local_userId);

            // Notifier tous les joueurs du résultat
            await Clients.Group($"game_{local_session.Id}").SendAsync("PlayerSubmitted", 
                local_user?.UserName, local_isCorrect, local_pointsEarned, local_player.Score);
        }

        /// <summary>
        /// Obtenir le classement actuel
        /// </summary>
        public async Task<object> GetLeaderboard(string gameCode)
        {
            var local_session = await _dbContext.GameSessions
                .Include(gs => gs.Players)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(gs => gs.Code == gameCode);

            if (local_session == null)
                throw new HubException("Game not found");

            var local_leaderboard = local_session.Players
                .OrderByDescending(p => p.Score)
                .Select(p => new { 
                    p.User!.UserName, 
                    p.Score, 
                    p.IsConnected 
                })
                .ToList();

            return local_leaderboard;
        }

        /// <summary>
        /// Passer au round suivant (MJ uniquement)
        /// </summary>
        public async Task NextRound(string gameCode)
        {
            var local_userId = Context.UserIdentifier;
            var local_session = await _dbContext.GameSessions
                .FirstOrDefaultAsync(gs => gs.Code == gameCode && gs.GameMasterId == local_userId);

            if (local_session == null)
                throw new HubException("Game not found or you are not the game master");

            local_session.CurrentRound++;
            await _dbContext.SaveChangesAsync();

            await Clients.Group($"game_{local_session.Id}").SendAsync("RoundChanged", local_session.CurrentRound);
            await StartRound(gameCode);
        }
    }
}
