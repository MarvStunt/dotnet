using System.ComponentModel.DataAnnotations;

namespace Gauniv.GameServer.Data
{
    public enum GameSessionStatus
    {
        Waiting = 0,
        InProgress = 1,
        Finished = 2
    }

    public class GameSession
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Code { get; set; } = GenerateCode();

        [Required]
        public string GameMasterName { get; set; } = string.Empty;

        public GameSessionStatus Status { get; set; } = GameSessionStatus.Waiting;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public int CurrentRound { get; set; } = 0;
        public int GridSize { get; set; } = 4;

        public ICollection<GamePlayer> Players { get; set; } = new List<GamePlayer>();
        public ICollection<GameRound> Rounds { get; set; } = new List<GameRound>();

        private static string GenerateCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    public class GamePlayer
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string GameSessionId { get; set; } = string.Empty;
        public GameSession? GameSession { get; set; }

        [Required]
        public string PlayerName { get; set; } = string.Empty;

        public int Score { get; set; } = 0;
        public bool IsConnected { get; set; } = true;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public string? ConnectionId { get; set; }
    }

    public class GameRound
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string GameSessionId { get; set; } = string.Empty;
        public GameSession? GameSession { get; set; }

        public int RoundNumber { get; set; }

        [Required]
        public string Pattern { get; set; } = "[]";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PlayerAttempt> PlayerAttempts { get; set; } = new List<PlayerAttempt>();
    }

    public class PlayerAttempt
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string GameRoundId { get; set; } = string.Empty;
        public GameRound? GameRound { get; set; }

        [Required]
        public string GamePlayerId { get; set; } = string.Empty;
        public GamePlayer? GamePlayer { get; set; }

        [Required]
        public string Attempt { get; set; } = "[]";

        public bool IsCorrect { get; set; } = false;
        public int PointsEarned { get; set; } = 0;
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public long ReactionTimeMs { get; set; } = 0;
    }
}
