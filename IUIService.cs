namespace Forestline.UI;

public interface IUIService
{
    T SpawnUIItem<T>(string message) where T : UnityEngine.Component;
}
