using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Wegs_Cafe.Helpers;

/// <summary>
/// ObservableCollection with batch ReplaceAll support so CollectionChanged fires once per refresh
/// instead of once per item, eliminating UI stutter on large menu/inventory lists.
/// </summary>
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        if (items != null)
        {
            foreach (var item in items)
                Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
