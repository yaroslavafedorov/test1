using System;
using System.Collections.Generic;
using Forestline.Core.EventSystem.Network;
using Mirror;
using UnityEngine;

namespace Forestline.Core.EventSystem;

/// <summary>
/// Управляет локальными подписками и маршрутизацией событий.
/// Полностью поддерживает автономный (оффлайн) режим работы.
/// </summary>
public class EventBroker : IEventBroker
{
    private readonly Dictionary<Type, object> _subscribers = new();

    public EventBroker()
    {
        // Мягкая привязка к сетевому слою Mirror
        if (GenericEventSystem.Instance != null)
        {
            GenericEventSystem.Instance.OnNetworkEventReceived += OnNetworkEventReceived;
        }
    }

    public void Subscribe<T>(Action<T> handler) where T : struct, IEvent
    {
        var type = typeof(T);
        if (!_subscribers.ContainsKey(type))
            _subscribers[type] = null;
        
        _subscribers[type] = (Action<T>)_subscribers[type] + handler;
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var currentHandler))
        {
            _subscribers[type] = (Action<T>)currentHandler - handler;
        }
    }

    public void Publish<T>(T forestlineEvent) where T : struct, IEvent
    {
        // 1. Офф-лайн режим
        if (GenericEventSystem.Instance == null || (!NetworkServer.active && !NetworkClient.isConnected))
        {
            TriggerLocally(forestlineEvent);
            return;
        }

        // 2. Он-лайн режим
        if (NetworkServer.active)
        {
            // Оптимизация трафика
            NetworkWriter writer = NetworkWriterPool.Get();
            writer.Write(forestlineEvent);
            
            var message = new NetworkEventMessage
            {
                EventTypeCode = typeof(T).AssemblyQualifiedName, 
                Payload = writer.ToArraySegment()
            };

            GenericEventSystem.Instance.BroadcastToAll(message);
            
            NetworkWriterPool.Return(writer);
        }
        else if (NetworkClient.isConnected)
        {
            // Обычный клиент не имеет права спавнить глобальные события без ведома сервера
            Debug.LogWarning($"[EventBroker] Клиент попытался вызвать сетевое событие {typeof(T).Name} напрямую. Публикация отклонена.");
        }
    }

    private void OnNetworkEventReceived(NetworkEventMessage message)
    {
        // Безопасное извлечение типа из любой сборки проекта
        Type type = Type.GetType(message.EventTypeCode);
        if (type == null)
        {
            Debug.LogError($"[EventBroker] Ошибка: Не удалось распознать тип события '{message.EventTypeCode}'");
            return;
        }

        // Если на этом клиенте никто не подписан на данный тип события — игнорируем пакет
        if (!_subscribers.ContainsKey(type)) return;

        NetworkReader reader = NetworkReaderPool.Get(message.Payload);
        
        try
        {
            // Безопасный поиск Generic-метода чтения структуры из Mirror Reader
            var readerExtensions = typeof(NetworkReaderExtensions);
            var readMethod = readerExtensions.GetMethod(nameof(NetworkReaderExtensions.Read), new[] { typeof(NetworkReader) });
            
            if (readMethod == null)
            {
                // Обрабтка для старых/кастомных версий Mirror, если расширение не найдено
                readMethod = typeof(NetworkReader).GetMethod(nameof(NetworkReader.Read), Type.EmptyTypes);
            }

            var closedReadMethod = readMethod.MakeGenericMethod(type);
            object deserializedEvent = closedReadMethod.Invoke(null, new object[] { reader });

            // Динамический вызов локальных подписчиков
            var triggerMethod = typeof(EventBroker)
                .GetMethod(nameof(TriggerLocally), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.MakeGenericMethod(type);

            triggerMethod?.Invoke(this, new[] { deserializedEvent });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EventBroker] Критическая ошибка обработки сетевого события {type.Name}: {ex.InnerException?.Message ?? ex.Message}");
        }
        finally
        {
            NetworkReaderPool.Return(reader);
        }
    }

    private void TriggerLocally<T>(T forestlineEvent) where T : struct, IEvent
    {
        if (_subscribers.TryGetValue(typeof(T), out var handlers) && handlers is Action<T> action)
        {
            action.Invoke(forestlineEvent);
        }
    }
}
