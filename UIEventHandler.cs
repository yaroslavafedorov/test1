using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Forestline.UI;

namespace Forestline.Core.EventSystem.UI;

/// <summary>
/// Базовый класс для всех UI-обработчиков в системе
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class UIEventHandler<T> : IEventHandler<T>, IDisposable where T : struct, IEvent
{
    protected readonly IUIService UiService;
    private readonly IEventBroker _eventBroker;
    
    // Потокобезопасная внутренняя очередь для предотвращения стирания UI-событий при лимитах
    private readonly Queue<T> _pendingEvents = new();
    private bool _isProcessingQueue;

    protected UIEventHandler(IUIService uiService, IEventBroker eventBroker)
    {
        UiService = uiService;
        _eventBroker = eventBroker;
        
        // Автоматическая подписка на брокер событий при создании объекта через Zenject
        _eventBroker.Subscribe<T>(Handle);
    }

    public void Handle(T gameEvent)
    {
        _pendingEvents.Enqueue(gameEvent);
        ProcessQueue().Forget();
    }

    private async UniTaskVoid ProcessQueue()
    {
        if (_isProcessingQueue) return;
        _isProcessingQueue = true;

        while (_pendingEvents.Count > 0)
        {
            var nextEvent = _pendingEvents.Peek();
            
            // Вызываем абстрактный метод отрисовки конкретного UI элемента
            bool isDisplayedSuccessfully = await RenderUI(nextEvent);

            if (isDisplayedSuccessfully)
            {
                _pendingEvents.Dequeue(); // Удаляем из очереди только при успешном спавне
            }
            else
            {
                // Если UI-сервис переполнен, ждем один кадр и пробуем снова (событие не теряется)
                await UniTask.Yield();
            }
        }

        _isProcessingQueue = false;
    }

    // Реализуется в конкретных UI окнах/панелях
    protected abstract UniTask<bool> RenderUI(T gameEvent);

    public virtual void Dispose()
    {
        _eventBroker.Unsubscribe<T>(Handle);
    }
}
