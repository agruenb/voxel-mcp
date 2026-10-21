namespace VoxelKing.Core.Common;

using System;

/// <summary>
/// Synchronous, typed event bus for decoupled messaging.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribers synchronously.
    /// </summary>
    /// <typeparam name="T">The type of the event.</typeparam>
    /// <param name="eventData">The event instance to broadcast.</param>
    void Publish<T>(T eventData) where T : notnull;

    /// <summary>
    /// Subscribes a synchronous handler for events of type T.
    /// </summary>
    /// <typeparam name="T">The type of the event to listen for.</typeparam>
    /// <param name="handler">The handler invoked when event T is published.</param>
    /// <returns>An IDisposable that unregisters the handler when disposed.</returns>
    IDisposable Subscribe<T>(Action<T> handler) where T : notnull;
}
