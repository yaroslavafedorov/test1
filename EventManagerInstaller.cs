using Forestline.Core.EventSystem.Network;
using Forestline.UI;
using UnityEngine;
using Zenject;

namespace Forestline.Core.EventSystem.DI;

public class EventManagerInstaller : MonoInstaller
{
    [SerializeField] private GenericEventSystem genericEventSystemPrefab;

    public override void InstallBindings()
    {
        // Регистрация UI-сервиса
        Container.Bind<IUIService>().To<UIServiceImplementation>().AsSingle().NonLazy();

        // Спавн и регистрация транспортного сетевого слоя Mirror
        if (genericEventSystemPrefab != null)
        {
            var networkSystemInstance = Container
                .InstantiatePrefabForComponent<GenericEventSystem>(genericEventSystemPrefab);
            
            Container.Bind<GenericEventSystem>().FromInstance(networkSystemInstance).AsSingle().NonLazy();
        }

        // Регистрация Брокера Событий 
        Container.Bind<IEventBroker>().To<EventBroker>().AsSingle().NonLazy();

        // Групповая регистрация всех UI-обработчиков
        // Сканирует текущую сборку, находит любые классы (например, QuestUIHandler, LootUIHandler),
        // реализующие маркер IEventHandlerMarker, регистрирует их интерфейсы и создает экземпляры как Singletons.
        Container.BindInterfacesAndSelfToAllClassesImplementing<IEventHandlerMarker>()
            .ToAccountForWindowsStoreExpress() // Обеспечивает совместимость с UWP/IL2CPP компиляцией
            .AsSingle()
            .NonLazy(); // NonLazy гарантирует, что конструкторы вызовутся сразу и подписка на брокер активируется
    }
}
