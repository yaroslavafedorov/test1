using System;
using System.Collections.Generic;
using Forestline.Core.EventSystem.Network;
using Mirror;
using UnityEngine;

namespace Forestline.Core.EventSystem;

public class EventBroker : IEventBroker, IDisposable
{
    private static readonly Dictionary<int, Type> _typeRegistry = new();

    private readonly GenericEventSystem _networkSystem;
    private readonly Dictionary<Type, object> _subscribers = new();

    public EventBroker(GenericEventSystem networkSystem)
    {
        _networkSystem = networkSystem;

        if (_networkSystem != null)
        {
            _networkSystem.OnNetworkEventReceived += OnNetworkEventReceived;
        }

        var type = typeof(MyEvent);
        _typeRegistry[type.FullName.GetStableHashCode()] = type;
    }

    public void Subscribe<T>(Action<T> handler) where T : struct, IEvent
    {
        var type = typeof(T);
        if (!_subscribers.TryGetValue(type, out var currentHandler))
        {
            _subscribers[type] = handler;
            return;
        }

        _subscribers[type] = (Action<T>)currentHandler + handler;
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var currentHandler))
        {
            var updatedHandler = (Action<T>)currentHandler - handler;
            
            if (updatedHandler == null)
                _subscribers.Remove(type);
            else
                _subscribers[type] = updatedHandler;
        }
    }

    public void Publish<T>(T forestlineEvent) where T : struct, IEvent
    {
        var isOffline = _networkSystem == null || (!NetworkServer.active && !NetworkClient.isConnected);
        if (isOffline)
        {
            TriggerLocally(forestlineEvent);
            return;
        }

        if (NetworkServer.active)
        {
            // Используем стандартный пул Mirror без ручной сериализации полей
            using (var writer = NetworkWriterPool.Get())
            {
                writer.Write(forestlineEvent);
                
                var message = new NetworkEventMessage
                {
                    EventTypeId = typeof(T).FullName.GetStableHashCode(), 
                    Payload = writer.ToArraySegment()
                };

                _networkSystem.BroadcastToAll(message);
            }
        }
        else if (NetworkClient.isConnected)
        {
            Debug.LogWarning($"[EventBroker] Client tried to publish network event {typeof(T).Name} directly. Rejected.");
        }
    }

    private void OnNetworkEventReceived(NetworkEventMessage message)
    {
        if (!_typeRegistry.TryGetValue(message.EventTypeId, out Type type) || !_subscribers.ContainsKey(type)) 
            return;

        using (var reader = NetworkReaderPool.Get(message.Payload))
        {
            try
            {
                var readMethod = typeof(NetworkReaderExtensions)
                    .GetMethod(nameof(NetworkReaderExtensions.Read), new[] { typeof(NetworkReader) })
                    ?.MakeGenericMethod(type);

                if (readMethod == null)
                    throw new InvalidOperationException($"Mirror Read<{type.Name}> extension method not found.");

                object deserializedEvent = readMethod.Invoke(null, new object[] { reader });

                // Вызываем локальных подписчиков через приватный generic-метод
                var triggerMethod = typeof(EventBroker)
                    .GetMethod(nameof(TriggerLocally), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.MakeGenericMethod(type);

                triggerMethod?.Invoke(this, new[] { deserializedEvent });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventBroker] Failed to process network event {type.Name}: {ex.GetBaseException().Message}");
            }
        }
    }

    private void TriggerLocally<T>(T forestlineEvent) where T : struct, IEvent
    {
        if (_subscribers.TryGetValue(typeof(T), out var handlers) && handlers is Action<T> action)
        {
            action.Invoke(forestlineEvent);
        }
    }

    public void Dispose()
    {
        if (_networkSystem != null)
        {
            _networkSystem.OnNetworkEventReceived -= OnNetworkEventReceived;
        }
    }
}
