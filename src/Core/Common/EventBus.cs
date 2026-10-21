namespace VoxelKing.Core.Common;

using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Synchronous, thread-safe implementation of IEventBus.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly object _lock = new();
    private readonly Dictionary<Type, List<object>> _subscribers = new();

    public IDisposable Subscribe<T>(Action<T> handler) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_lock)
        {
            var eventType = typeof(T);
            if (!_subscribers.TryGetValue(eventType, out var list))
            {
                list = new List<object>();
                _subscribers[eventType] = list;
            }

            list.Add(handler);
        }

        return new SubscriptionToken(() => Unsubscribe(handler));
    }

    private void Unsubscribe<T>(Action<T> handler) where T : notnull
    {
        lock (_lock)
        {
            var eventType = typeof(T);
            if (_subscribers.TryGetValue(eventType, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0)
                {
                    _subscribers.Remove(eventType);
                }
            }
        }
    }

    public void Publish<T>(T eventData) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Action<T>[] handlers;
        lock (_lock)
        {
            var eventType = typeof(T);
            if (!_subscribers.TryGetValue(eventType, out var list) || list.Count == 0)
            {
                return;
            }

            handlers = new Action<T>[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                handlers[i] = (Action<T>)list[i];
            }
        }

        List<Exception>? exceptions = null;
        for (int i = 0; i < handlers.Length; i++)
        {
            try
            {
                handlers[i](eventData);
            }
            catch (Exception ex)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(ex);
            }
        }

        if (exceptions != null)
        {
            if (exceptions.Count == 1)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            }

            throw new AggregateException("One or more event handlers failed during synchronous dispatch.", exceptions);
        }
    }

    public int GetSubscriberCount<T>() where T : notnull
    {
        lock (_lock)
        {
            return _subscribers.TryGetValue(typeof(T), out var list) ? list.Count : 0;
        }
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private Action? _unsubscribe;

        public SubscriptionToken(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            var action = Interlocked.Exchange(ref _unsubscribe, null);
            action?.Invoke();
        }
    }
}
