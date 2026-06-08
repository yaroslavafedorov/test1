using System;
using Mirror;
using UnityEngine;

namespace Forestline.Core.EventSystem.Network;

[RequireComponent(typeof(NetworkIdentity))]
public class GenericEventSystem : NetworkBehaviour
{
    public event Action<NetworkEventMessage> OnNetworkEventReceived;

    public override void OnStartServer()
    {
        NetworkServer.RegisterHandler<NetworkEventMessage>(OnClientMessageReceived);
    }

    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<NetworkEventMessage>(OnServerMessageReceived);
    }

    [Server]
    public void BroadcastToAll(NetworkEventMessage message)
    {
        NetworkServer.SendToAll(message, Channels.Reliable);
        
        if (NetworkClient.active)
        {
            OnNetworkEventReceived?.Invoke(message);
        }
    }

    private void OnServerMessageReceived(NetworkEventMessage message)
    {
        if (isServer) return;
        OnNetworkEventReceived?.Invoke(message);
    }

    private void OnClientMessageReceived(NetworkConnectionToClient conn, NetworkEventMessage message)
    {
        // При необходимости здесь можно добавить валидацию отправителя (conn)
        BroadcastToAll(message);
    }
}
