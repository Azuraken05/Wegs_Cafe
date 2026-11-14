using Firebase.Database.Query;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Generic CRUD helper for one Firebase node (e.g. "menu", "orders").
/// Usage: new FirebaseRepository&lt;Product&gt;(firebase, Constants.Nodes.Menu)
/// </summary>
public class FirebaseRepository<T> where T : BaseModel
{
    private readonly FirebaseService _firebase;
    private readonly string _node;

    public FirebaseRepository(FirebaseService firebase, string node)
    {
        _firebase = firebase;
        _node = node;
    }

    private ChildQuery Root => _firebase.Db.Child(_node);

    public async Task<List<T>> GetAllAsync()
    {
        var items = await Root.OnceAsync<T>();
        return items.Select(x =>
        {
            x.Object.Id = x.Key;
            return x.Object;
        }).ToList();
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        var item = await Root.Child(id).OnceSingleAsync<T>();
        if (item != null) item.Id = id;
        return item;
    }

    /// <summary>Adds a new item. Uses item.Id as key if set, otherwise Firebase generates one.</summary>
    public async Task<T> AddAsync(T item)
    {
        item.CreatedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(item.Id))
        {
            var result = await Root.PostAsync(item);
            item.Id = result.Key;
        }
        else
        {
            await Root.Child(item.Id).PutAsync(item);
        }
        return item;
    }

    public async Task UpdateAsync(T item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
            throw new ArgumentException("Item must have an Id to be updated.");

        item.UpdatedAt = DateTime.UtcNow;
        await Root.Child(item.Id).PutAsync(item);
    }

    public Task DeleteAsync(string id) => Root.Child(id).DeleteAsync();

    /// <summary>Real-time updates (fires whenever an item is added/changed/removed).</summary>
    public IObservable<Firebase.Database.Streaming.FirebaseEvent<T>> Observe() => Root.AsObservable<T>();
}
