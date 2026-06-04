namespace Forestline.Core.EventSystem;

/// <summary>
/// Интерфейс типизированного обработчика
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IEventHandler<T> where T : struct, IEvent
{
    void Handle(T forestlineEvent);
}