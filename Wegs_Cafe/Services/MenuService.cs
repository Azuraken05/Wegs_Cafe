using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

public class MenuService
{
    public FirebaseRepository<Product> Items { get; }
    public FirebaseRepository<Category> Categories { get; }

    public MenuService(FirebaseService firebase)
    {
        Items = new FirebaseRepository<Product>(firebase, Constants.Nodes.Menu);
        Categories = new FirebaseRepository<Category>(firebase, Constants.Nodes.Categories);
    }

    public async Task<List<Product>> GetAvailableProductsAsync()
        => (await Items.GetAllAsync()).Where(i => i.IsAvailable).ToList();

    public async Task<List<Product>> GetProductsByCategoryAsync(string categoryId)
        => (await Items.GetAllAsync()).Where(i => i.CategoryId == categoryId).ToList();

    public async Task<List<Category>> GetCategoriesAsync()
        => (await Categories.GetAllAsync()).OrderBy(c => c.SortOrder).ToList();
}
