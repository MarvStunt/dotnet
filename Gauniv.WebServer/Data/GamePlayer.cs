#region Header
#endregion
using System.ComponentModel.DataAnnotations;

namespace Gauniv.WebServer.Data
{
    public class GamePlayer
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string GameSessionId { get; set; } = string.Empty;
        
        public GameSession? GameSession { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        public User? User { get; set; }
        
        public int Score { get; set; } = 0;
        
        public bool IsConnected { get; set; } = true;
        
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        
        public string? ConnectionId { get; set; }
    }
}
