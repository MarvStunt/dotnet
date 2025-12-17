#region Header
#endregion
using System.ComponentModel.DataAnnotations;

namespace Gauniv.WebServer.Data
{
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
        
        // Player attempt in JSON: [0, 5, 12, ...]
        [Required]
        public string Attempt { get; set; } = "[]";
        
        public bool IsCorrect { get; set; } = false;
        
        public int PointsEarned { get; set; } = 0;
        
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        
        // Reaction time in milliseconds
        public long ReactionTimeMs { get; set; } = 0;
    }
}
