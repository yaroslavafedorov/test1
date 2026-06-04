using System;
using Mirror;
using UnityEngine;

namespace Forestline.Core.EventSystem.Network;

[RequireComponent(typeof(NetworkIdentity))]
public class GenericEventSystem : NetworkBehaviour
{
    private static GenericEventSystem _instance;
    public static GenericEventSystem Instance => _instance;

    public event Action<NetworkEventMessage> OnNetworkEventReceived;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    public override void OnStartServer()
    {
        NetworkServer.RegisterHandler<NetworkEventMessage>(OnClientMessageReceived, false);
    }

    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<NetworkEventMessage>(OnServerMessageReceived, false);
    }

    [Server]
    public void BroadcastToAll(NetworkEventMessage message)
    {
        // Оптимизация: один сетевой пакет гарантированной доставки вместо RPC и SyncList
        NetworkServer.SendToAll(message, Channels.Reliable);
        
        // Если сервер запущен в режиме Host, локальный клиент обрабатывает событие сразу
        if (NetworkClient.active)
        {
            OnNetworkEventReceived?.Invoke(message);
        }
    }

    private void OnServerMessageReceived(NetworkEventMessage message)
    {
        if (isServer) return; // Хост уже обработал событие локально в BroadcastToAll
        OnNetworkEventReceived?.Invoke(message);
    }

    private void OnClientMessageReceived(NetworkConnectionToClient conn, NetworkEventMessage message)
    {
        // Точка валидации: здесь сервер может проверить права клиента перед ретрансляцией
        BroadcastToAll(message);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
