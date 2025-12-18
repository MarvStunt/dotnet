#region Licence
#endregion
using Gauniv.WebServer.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gauniv.WebServer.Api
{
    [Route("api/1.0.0/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class StatsController(ApplicationDbContext applicationDbContext) : ControllerBase
    {
        private readonly ApplicationDbContext applicationDbContext = applicationDbContext;

        [HttpGet("revenue-by-month")]
        public async Task<IActionResult> GetRevenueByMonth()
        {
            var userGames = await applicationDbContext
                .UserGames.Include(ug => ug.Game)
                .ToListAsync();

            // Group by year and month, then calculate revenue
            var revenueByMonth = userGames
                .GroupBy(ug => new { ug.PurchaseDate.Year, ug.PurchaseDate.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = (int)g.Sum(ug => ug.Game.Price),
                    Count = g.Count()
                })
                .ToList();

            // Build data for the last 6 months
            var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            var last6Months = new List<object>();

            for (int i = 5; i >= 0; i--)
            {
                var monthDate = DateTime.UtcNow.AddMonths(-i);
                var monthName = months[monthDate.Month - 1];
                var revenue = revenueByMonth
                    .Where(r => r.Year == monthDate.Year && r.Month == monthDate.Month)
                    .FirstOrDefault();

                last6Months.Add(new
                {
                    month = monthName,
                    revenue = revenue?.Revenue ?? 0
                });
            }

            return Ok(last6Months);
        }

        [HttpGet("top-selling-games")]
        public async Task<IActionResult> GetTopSellingGames()
        {
            var topGames = await applicationDbContext
                .UserGames.Include(ug => ug.Game)
                .GroupBy(ug => ug.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    GameName = g.FirstOrDefault()!.Game.Name,
                    Sales = g.Count(),
                    TotalRevenue = (int)g.Sum(ug => ug.Game.Price),
                    Price = g.FirstOrDefault()!.Game.Price
                })
                .OrderByDescending(g => g.Sales)
                .Take(5)
                .ToListAsync();

            return Ok(topGames);
        }

        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var games = await applicationDbContext.Games.ToListAsync();
            var users = await applicationDbContext.Users.ToListAsync();
            var userGames = await applicationDbContext
                .UserGames.Include(ug => ug.Game)
                .ToListAsync();

            decimal totalRevenue = userGames.Sum(ug => ug.Game.Price);
            int totalGames = games.Count;
            int totalUsers = users.Count;
            int activeUsers = users.Count;

            return Ok(new
            {
                totalRevenue,
                totalGames,
                totalUsers,
                activeUsers
            });
        }
    }
}
