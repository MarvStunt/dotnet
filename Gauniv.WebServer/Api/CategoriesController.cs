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
    public class CategoriesController(ApplicationDbContext appDbContext) : ControllerBase
    {
        private readonly ApplicationDbContext appDbContext = appDbContext;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllCategories()
        {
            var categories = await appDbContext
                .Categories.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Description,
                    GameCount = c.GameCategories.Count,
                })
                .ToListAsync();

            return Ok(categories);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await appDbContext
                .Categories.Include(c => c.GameCategories)
                    .ThenInclude(gc => gc.Game)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return NotFound(new { message = "Catégorie non trouvée" });
            }

            return Ok(
                new
                {
                    category.Id,
                    category.Name,
                    category.Description,
                    Games = category.GameCategories.Select(gc => new
                    {
                        gc.Game.Id,
                        gc.Game.Name,
                        gc.Game.Price,
                    }),
                }
            );
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new { message = "Le nom de la catégorie est requis" });
            }

            var existingCategory = await appDbContext.Categories.FirstOrDefaultAsync(c =>
                c.Name.ToLower() == dto.Name.ToLower()
            );

            if (existingCategory != null)
            {
                return BadRequest(new { message = "Une catégorie avec ce nom existe déjà" });
            }

            var category = new Category
            {
                Name = dto.Name,
                Description = dto.Description ?? string.Empty,
            };

            appDbContext.Categories.Add(category);
            await appDbContext.SaveChangesAsync();
            return CreatedAtAction(
                nameof(GetCategory),
                new { id = category.Id },
                new
                {
                    category.Id,
                    category.Name,
                    category.Description,
                    message = "Catégorie créée avec succès",
                }
            );
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryUpdateDto dto)
        {
            var category = await appDbContext.Categories.FindAsync(id);

            if (category == null)
            {
                return NotFound(new { message = "Catégorie non trouvée" });
            }

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var existingCategory = await appDbContext.Categories.FirstOrDefaultAsync(c =>
                    c.Name.ToLower() == dto.Name.ToLower() && c.Id != id
                );

                if (existingCategory != null)
                {
                    return BadRequest(
                        new { message = "Une autre catégorie avec ce nom existe déjà" }
                    );
                }

                category.Name = dto.Name;
            }

            if (dto.Description != null)
            {
                category.Description = dto.Description;
            }

            await appDbContext.SaveChangesAsync();

            return Ok(
                new
                {
                    category.Id,
                    category.Name,
                    category.Description,
                    message = "Catégorie mise à jour avec succès",
                }
            );
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await appDbContext
                .Categories.Include(c => c.GameCategories)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                return NotFound(new { message = "Catégorie non trouvée" });
            }

            if (category.GameCategories.Any())
            {
                return BadRequest(
                    new
                    {
                        message = $"Impossible de supprimer cette catégorie : {category.GameCategories.Count} jeu(x) y sont associé(s)",
                        gameCount = category.GameCategories.Count,
                    }
                );
            }

            appDbContext.Categories.Remove(category);
            await appDbContext.SaveChangesAsync();

            return Ok(new { message = $"Catégorie '{category.Name}' supprimée avec succès" });
        }
    }

    public class CategoryCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CategoryUpdateDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
