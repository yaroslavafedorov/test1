namespace Forestline.Core.EventSystem;

/// <summary>
/// Интерфейс для публикации и подписки.
/// Изолирует от сетевой реализации.
/// </summary>
public interface IEventBroker
{
    void Publish<T>(T forestlineEvent) where T : struct, IEvent;
    void Subscribe<T>(Action<T> handler) where T : struct, IEvent;
    void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent;
}