#region Licence
#endregion
using Gauniv.WebServer.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gauniv.WebServer.Api
{
    [Route("api/1.0.0/[controller]")]
    [ApiController]
    public class UsersController(
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager
    ) : ControllerBase
    {
        private readonly UserManager<User> userManager = userManager;
        private readonly RoleManager<IdentityRole> roleManager = roleManager;

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await userManager
                .Users.Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.UserName,
                })
                .ToListAsync();

            var usersWithRoles = new List<object>();

            foreach (var user in users)
            {
                var userEntity = await userManager.FindByIdAsync(user.Id);
                var roles = await userManager.GetRolesAsync(userEntity!);

                usersWithRoles.Add(
                    new
                    {
                        user.Id,
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        user.UserName,
                        Roles = roles,
                    }
                );
            }

            return Ok(usersWithRoles);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { message = "Utilisateur non trouvé" });
            }

            var roles = await userManager.GetRolesAsync(user);
            return Ok(
                new
                {
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.UserName,
                    Roles = roles,
                }
            );
        }

        [HttpPost("{userId}/roles/{roleName}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole(string userId, string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                return BadRequest(new { message = $"Le rôle '{roleName}' n'existe pas" });
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Utilisateur non trouvé" });
            }

            if (await userManager.IsInRoleAsync(user, roleName))
            {
                return BadRequest(new { message = $"L'utilisateur a déjà le rôle '{roleName}'" });
            }

            var result = await userManager.AddToRoleAsync(user, roleName);
            if (result.Succeeded)
            {
                return Ok(
                    new { message = $"Rôle '{roleName}' assigné avec succès à {user.Email}" }
                );
            }

            return BadRequest(
                new { message = "Erreur lors de l'assignation du rôle", errors = result.Errors }
            );
        }

        [HttpDelete("{userId}/roles/{roleName}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveRole(string userId, string roleName)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "Utilisateur non trouvé" });
            }

            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                return BadRequest(new { message = $"L'utilisateur n'a pas le rôle '{roleName}'" });
            }

            var result = await userManager.RemoveFromRoleAsync(user, roleName);
            if (result.Succeeded)
            {
                return Ok(
                    new { message = $"Rôle '{roleName}' retiré avec succès de {user.Email}" }
                );
            }

            return BadRequest(
                new { message = "Erreur lors du retrait du rôle", errors = result.Errors }
            );
        }

        [HttpGet("roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = await roleManager.Roles.Select(r => new { r.Id, r.Name }).ToListAsync();

            return Ok(roles);
        }
    }
}
