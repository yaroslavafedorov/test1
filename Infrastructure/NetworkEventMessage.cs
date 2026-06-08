using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Forestline.Core.EventSystem.Network;

/// <summary>
/// Легковесная сетевая обертка. Передается по Reliable каналу.
/// </summary>
public struct NetworkEventMessage : NetworkMessage
{
    /// <summary>
    /// Тип события
    /// </summary>
    public int EventTypeId;

    /// <summary>
    /// Сжатые бинарные данные
    /// </summary>
    public ArraySegment<byte> Payload;
}