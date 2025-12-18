#region Header
#endregion
using System.ComponentModel.DataAnnotations;

namespace Gauniv.WebServer.Data
{
    public class UserGame
    {
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        public int GameId { get; set; }
        public Game Game { get; set; } = null!;

        [Required]
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    }
}
