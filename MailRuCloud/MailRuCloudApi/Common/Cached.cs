﻿using System;

namespace YaR.Clouds.Common
{
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

        private readonly object _refreshLock = new();

        private void RefreshValueIfNeeded()
        {
            if (DateTime.Now < _expiration) 
                return;

            lock (_refreshLock)
            {
                if (DateTime.Now < _expiration) 
                    return;

                T oldValue =  _value is { IsValueCreated: true } ? _value.Value : default;
                _value = new Lazy<T>(() => _valueFactory(oldValue));

                var duration = _duration(_value.Value);
                _expiration = duration == TimeSpan.MaxValue 
                    ? DateTime.MaxValue
                    : DateTime.Now.Add(duration);
            }
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public void Expire()
        {
            lock (_refreshLock)
            {
                _expiration = DateTime.MinValue;
            }
        }
    }
}