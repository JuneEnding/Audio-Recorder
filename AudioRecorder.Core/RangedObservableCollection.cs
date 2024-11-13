using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace AudioRecorder.Core;

public class RangedObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public RangedObservableCollection() { }

    public RangedObservableCollection(IEnumerable<T> list) : base(list) { }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }

    public void AddRange(IEnumerable<T> list)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        DisableOnCollectionChanged();

        foreach (var item in list)
            Add(item);

        EnableOnCollectionChanged();
    }

    public void RemoveRange(int start, int end)
    {
        DisableOnCollectionChanged();

        for (var i = end; i >= start; --i)
            RemoveAt(i);

        EnableOnCollectionChanged();
    }

    public void RemoveRange(IEnumerable<T> list)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        DisableOnCollectionChanged();

        foreach (var item in list)
            Remove(item);

        EnableOnCollectionChanged();
    }

    public void RemoveAll(Predicate<T> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        DisableOnCollectionChanged();

        for (var i = Count - 1; i >= 0; i--)
            if (predicate(this[i]))
                RemoveAt(i);

        EnableOnCollectionChanged();
    }

    public void Refresh(IList<T> list, bool suppressNotification = true)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        if (suppressNotification)
            DisableOnCollectionChanged();

        var toAdd = list.Except(this).ToList();
        var toRemove = this.Where(item => !list.Contains(item)).ToList();

        foreach (var item in toRemove)
            Remove(item);

        foreach (var item in toAdd)
            Add(item);

        if (suppressNotification)
            EnableOnCollectionChanged();
    }

    public void DisableOnCollectionChanged()
    {
        _suppressNotification = true;
    }

    public void EnableOnCollectionChanged()
    {
        _suppressNotification = false;
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}