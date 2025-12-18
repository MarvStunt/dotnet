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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.HighPerformance;
using Gauniv.WebServer.Data;
using Gauniv.WebServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging;
using X.PagedList.Extensions;

namespace Gauniv.WebServer.Controllers
{
    public class HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext applicationDbContext,
        UserManager<User> userManager
    ) : Controller
    {
        private readonly ILogger<HomeController> _logger = logger;
        private readonly ApplicationDbContext applicationDbContext = applicationDbContext;
        private readonly UserManager<User> userManager = userManager;

        public async Task<IActionResult> Index(
            string? search,
            int? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            int? minSize,
            bool? owned,
            int page = 1
        )
        {
            const int pageSize = 12; // 12 games per page (good for 4-column grid)
            
            var gamesQuery = applicationDbContext
                .Games.Include(g => g.GameCategories)
                    .ThenInclude(gc => gc.Category)
                .AsQueryable();

            // Filter by search name/description
            if (!string.IsNullOrEmpty(search))
            {
                gamesQuery = gamesQuery.Where(g =>
                    g.Name.Contains(search) || g.Description.Contains(search)
                );
            }

            // Filter by category
            if (categoryId.HasValue)
            {
                gamesQuery = gamesQuery.Where(g =>
                    g.GameCategories.Any(gc => gc.CategoryId == categoryId.Value)
                );
            }

            // Filter by price range
            if (minPrice.HasValue)
            {
                gamesQuery = gamesQuery.Where(g => g.Price >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                gamesQuery = gamesQuery.Where(g => g.Price <= maxPrice.Value);
            }

            // Filter by file size (in KB)
            if (minSize.HasValue && minSize.Value > 0)
            {
                var minSizeBytes = minSize.Value * 1024; // Convert KB to bytes
                gamesQuery = gamesQuery.Where(g => g.Payload.Length >= minSizeBytes);
            }

            // Filter by ownership status
            if (owned.HasValue && User.Identity?.IsAuthenticated == true)
            {
                var user = await userManager.GetUserAsync(User);
                if (user != null)
                {
                    if (owned.Value)
                    {
                        gamesQuery = gamesQuery.Where(g =>
                            g.UserGames.Any(ug => ug.UserId == user.Id)
                        );
                    }
                    else
                    {
                        gamesQuery = gamesQuery.Where(g =>
                            !g.UserGames.Any(ug => ug.UserId == user.Id)
                        );
                    }
                }
            }

            // Apply pagination
            var orderedGames = gamesQuery.OrderByDescending(g => g.Id);
            var pagedGames = (await orderedGames.ToListAsync()).ToPagedList(page, pageSize);
            
            // Get owned games for current user
            HashSet<int> ownedGameIds = new HashSet<int>();
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await userManager.GetUserAsync(User);
                if (user != null)
                {
                    ownedGameIds = await applicationDbContext
                        .UserGames.Where(ug => ug.UserId == user.Id)
                        .Select(ug => ug.GameId)
                        .ToHashSetAsync();
                }
            }

            // Set ViewBag for filter persistence
            ViewBag.Categories = await applicationDbContext.Categories.ToListAsync();
            ViewBag.OwnedGameIds = ownedGameIds;
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.MinSize = minSize;
            ViewBag.Owned = owned;

            return View(pagedGames);
        }

        [Authorize]
        public async Task<IActionResult> MyGames()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var myGames = await applicationDbContext
                .UserGames.Where(ug => ug.UserId == user.Id)
                .Include(ug => ug.Game)
                    .ThenInclude(g => g.GameCategories)
                        .ThenInclude(gc => gc.Category)
                .Select(ug => ug.Game)
                .ToListAsync();

            return View(myGames);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin()
        {
            var games = await applicationDbContext
                .Games.Include(g => g.GameCategories)
                    .ThenInclude(gc => gc.Category)
                .ToListAsync();

            var categories = await applicationDbContext.Categories.ToListAsync();
            var users = await applicationDbContext.Users.ToListAsync();
            
            // Calculate real statistics from purchases
            var userGames = await applicationDbContext
                .UserGames.Include(ug => ug.Game)
                .ToListAsync();
            
            decimal totalRevenue = userGames.Sum(ug => ug.Game.Price);
            int totalGames = games.Count;
            int totalUsers = users.Count;
            int activeUsers = users.Count; // You can refine this to count only active users (e.g., logged in last 30 days)
            
            ViewBag.Categories = categories;
            ViewBag.Stats = new
            {
                TotalRevenue = totalRevenue,
                TotalGames = totalGames,
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers
            };
            
            return View(games);
        }

        public async Task<IActionResult> Players()
        {
            var players = await applicationDbContext
                .Users.Include(u => u.PurchasedGames)
                .ToListAsync();

            return View(players);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(
                new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                }
            );
        }
    }
}
