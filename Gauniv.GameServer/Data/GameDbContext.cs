using Microsoft.EntityFrameworkCore;

namespace Gauniv.GameServer.Data
{
    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
        {
        }

        public DbSet<GameSession> GameSessions { get; set; }
        public DbSet<GamePlayer> GamePlayers { get; set; }
        public DbSet<GameRound> GameRounds { get; set; }
        public DbSet<PlayerAttempt> PlayerAttempts { get; set; }
    }
}
