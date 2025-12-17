#region Header
#endregion
using System.ComponentModel.DataAnnotations;

namespace Gauniv.WebServer.Data
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
        public string GameMasterId { get; set; } = string.Empty;  // User ID of the game master

        public User? GameMaster { get; set; } // User entity of the game master

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
}
