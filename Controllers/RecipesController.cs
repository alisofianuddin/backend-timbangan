using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipesController : ControllerBase
{
    private readonly AppDbContext _context;

    public RecipesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecipes()
    {
        var recipes = await _context.Recipes.ToListAsync();
        return Ok(recipes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRecipe(int id)
    {
        var recipe = await _context.Recipes.FindAsync(id);

        if (recipe == null)
            return NotFound(new { message = "Recipe not found" });

        return Ok(recipe);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRecipe([FromBody] Recipe recipe)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        recipe.CreatedAt = DateTime.UtcNow;
        recipe.UpdatedAt = DateTime.UtcNow;

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, recipe);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRecipe(int id, [FromBody] Recipe recipeUpdate)
    {
        if (id != recipeUpdate.Id)
            return BadRequest(new { message = "ID mismatch" });

        var recipe = await _context.Recipes.FindAsync(id);
        if (recipe == null)
            return NotFound(new { message = "Recipe not found" });

        recipe.NamaProduk = recipeUpdate.NamaProduk;
        recipe.BobotMinimal = recipeUpdate.BobotMinimal;
        recipe.BobotMaximal = recipeUpdate.BobotMaximal;
        recipe.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(recipe);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRecipe(int id)
    {
        var recipe = await _context.Recipes.FindAsync(id);
        if (recipe == null)
            return NotFound(new { message = "Recipe not found" });

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Recipe deleted successfully" });
    }
}
