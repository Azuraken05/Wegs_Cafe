using Wegs_Cafe.Models;

namespace Wegs_Cafe.ViewModels;

/// <summary>A product plus what the current user may do with it (keeps XAML bindings simple).</summary>
public class ProductRow
{
    public ProductRow(Product product, bool canEdit)
    {
        Product = product;
        CanEdit = canEdit;
    }

    public Product Product { get; }
    public bool CanEdit { get; }
}
