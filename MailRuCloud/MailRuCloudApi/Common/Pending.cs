using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace YaR.Clouds.Common;

class Pending<T> where T : class
{
    private readonly List<PendingItem<T>> _items = new();
    private readonly int _maxLocks;
    private readonly Func<T> _valueFactory;

    public Pending(int maxLocks, Func<T> valueFactory)
    {
        _maxLocks = maxLocks;
        _valueFactory = valueFactory;
    }

    private readonly SemaphoreSlim _locker = new SemaphoreSlim(1);

    public T Next(T current)
    {
        _locker.Wait();
        try
        {
            var item = current is null
                ? _items.FirstOrDefault(it => it.LockCount < _maxLocks)
                : _items.SkipWhile(it => !it.Equals(current)).Skip(1).FirstOrDefault(it => it.LockCount < _maxLocks);

            if (item is null)
                _items.Add(item = new PendingItem<T> { Item = _valueFactory(), LockCount = 0 });

            item.LockCount++;

            return item.Item;
        }
        finally
        {
            _locker.Release();
        }
    }

    public void Free(T value)
    {
        if (value is null)
            return;

        _locker.Wait();
        try
        {
            foreach (var item in _items)
            {
                if (item.Item.Equals(value))
                {
                    switch (item.LockCount)
                    {
                    case <= 0:
                        throw new Exception("Pending item count <= 0");
                    case > 0:
                        item.LockCount--;
                        break;
                    }
                }
            }
        }
        finally
        {
            _locker.Release();
        }
    }
}

class PendingItem<T>
{
    public T Item { get; set; }
    public int LockCount { get; set; }
}
