#region Header
#endregion
using System.ComponentModel.DataAnnotations;

namespace Gauniv.WebServer.Data
{
    public class GameRound
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string GameSessionId { get; set; } = string.Empty;
        
        public GameSession? GameSession { get; set; }
        
        public int RoundNumber { get; set; }
        
        // Pattern in JSON: [0, 5, 12, 3, ...] (indexes of the grid)
        [Required]
        public string Pattern { get; set; } = "[]";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<PlayerAttempt> PlayerAttempts { get; set; } = new List<PlayerAttempt>();
    }
}
