using System;
using System.Threading;

namespace YaR.Clouds.Common;

public class Cached<T>
{
    private readonly Func<T, TimeSpan> _duration;
    private DateTime _expiration;
    private Lazy<T> _value;
    private readonly Func<T, T> _valueFactory;

    public T Value
    {
        get
        {
            RefreshValueIfNeeded();
            return _value.Value;
        }
    }

    public Cached(Func<T, T> valueFactory, Func<T, TimeSpan> duration)
    {
        _duration = duration;
        _valueFactory = valueFactory;

        RefreshValueIfNeeded();
    }

    private readonly SemaphoreSlim _locker = new SemaphoreSlim(1);

    private void RefreshValueIfNeeded()
    {
        if (DateTime.Now < _expiration)
            return;

        _locker.Wait();
        try
        {
            if (DateTime.Now < _expiration)
                return;

            T oldValue = _value is { IsValueCreated: true } ? _value.Value : default;
            _value = new Lazy<T>(() => _valueFactory(oldValue));

            var duration = _duration(_value.Value);
            _expiration = duration == TimeSpan.MaxValue
                ? DateTime.MaxValue
                : DateTime.Now.Add(duration);
        }
        finally
        {
            _locker.Release();
        }
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public void Expire()
    {
        _locker.Wait();
        try
        {
            _expiration = DateTime.MinValue;
        }
        finally
        {
            _locker.Release();
        }
    }
}
