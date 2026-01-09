using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

/// <summary>
/// Example P2P packet sender/receiver from the Facepunch Steamworks tutorial.
/// Attach to a GameObject in your gameplay scene and replace the payload handling as needed.
/// </summary>
public class P2PGameObject : MonoBehaviour
{
    [SerializeField] private string periodicPayload = "heartbeat";
    [SerializeField] private float periodicIntervalSeconds = 0.4f;
    [SerializeField] private float droppedRetryIntervalSeconds = 1.25f;
    [SerializeField] private float receivePollIntervalSeconds = 0.05f;

    private static readonly List<byte[]> CachedMessages = new();
    private readonly List<byte[]> thingsThatFailedToSend = new();
    private readonly List<byte[]> retryQueueForThingsThatFailedToSend = new();
    private readonly List<byte[]> thingsThatFailedToHandle = new();
    private readonly List<byte[]> retryQueueForThingsThatFailedToHandle = new();

    // private void Awake()
    // {
    //     if (CachedMessages.Count > 0)
    //     {
    //         foreach (byte[] cachedMessage in CachedMessages)
    //         {
    //             thingsThatFailedToHandle.Add(cachedMessage);
    //         }

    //         CachedMessages.Clear();
    //         Invoke(nameof(HandleCachedMessages), 0.25f);
    //     }

    //     InvokeRepeating(nameof(SendDataOnInterval), 0f, periodicIntervalSeconds);
    //     InvokeRepeating(nameof(SendDroppedMessages), 1f, droppedRetryIntervalSeconds);
    //     InvokeRepeating(nameof(ReceiveDataPacket), 0f, receivePollIntervalSeconds);
    // }

    // private void SendDroppedMessages()
    // {
    //     if (SteamManager.Instance != null && SteamManager.Instance.currentLobby.MemberCount <= 1)
    //     {
    //         if (thingsThatFailedToSend.Count == 0)
    //         {
    //             return;
    //         }

    //         retryQueueForThingsThatFailedToSend.Clear();
    //         foreach (byte[] message in thingsThatFailedToSend)
    //         {
    //             retryQueueForThingsThatFailedToSend.Add(message);
    //         }

    //         thingsThatFailedToSend.Clear();
    //         foreach (byte[] message in retryQueueForThingsThatFailedToSend)
    //         {
    //             Debug.Log("Attempting to send dropped message");
    //             // bool sent = SteamNetworking.SendP2PPacket(OpponentId, message);
    //             bool sent = NetworkConnectionManager.Instance.SendToAll(message, 2,)
    //             if (!sent)
    //             {
    //                 thingsThatFailedToSend.Add(message);
    //             }
    //         }
    //     }
    // }

    // private void ReceiveDataPacket()
    // {
    //     while (SteamNetworking.IsP2PPacketAvailable())
    //     {
    //         P2Packet? packet = SteamNetworking.ReadP2PPacket();
    //         if (packet.HasValue)
    //         {
    //             HandleOpponentDataPacket(packet.Value.Data);
    //         }
    //     }
    // }

    // private void SendDataOnInterval()
    // {
    //     if (SteamManager.Instance != null && !SteamManager.Instance.LobbyPartnerDisconnected)
    //     {
    //         string dataToSend = periodicPayload;
    //         bool sent = SteamNetworking.SendP2PPacket(OpponentId, ConvertStringToByteArray(dataToSend));
    //         if (!sent)
    //         {
    //             bool sent2 = SteamNetworking.SendP2PPacket(OpponentId, ConvertStringToByteArray(dataToSend));
    //             if (!sent2)
    //             {
    //                 thingsThatFailedToSend.Add(ConvertStringToByteArray(dataToSend));
    //             }
    //         }
    //     }
    // }

    // public void SendAdHocData(string adHocData)
    // {
    //     if (SteamManager.Instance != null && !SteamManager.Instance.LobbyPartnerDisconnected)
    //     {
    //         string dataToSend = adHocData;
    //         bool sent = SteamNetworking.SendP2PPacket(OpponentId, ConvertStringToByteArray(dataToSend));
    //         if (!sent)
    //         {
    //             bool sent2 = SteamNetworking.SendP2PPacket(OpponentId, ConvertStringToByteArray(dataToSend));
    //             if (!sent2)
    //             {
    //                 thingsThatFailedToSend.Add(ConvertStringToByteArray(dataToSend));
    //             }
    //         }
    //     }
    // }

    // public static void CacheMessage(byte[] data)
    // {
    //     CachedMessages.Add(data);
    // }

    // private void HandleCachedMessages()
    // {
    //     try
    //     {
    //         if (thingsThatFailedToHandle.Count == 0)
    //         {
    //             return;
    //         }

    //         retryQueueForThingsThatFailedToHandle.Clear();
    //         foreach (byte[] message in thingsThatFailedToHandle)
    //         {
    //             retryQueueForThingsThatFailedToHandle.Add(message);
    //         }

    //         thingsThatFailedToHandle.Clear();
    //         foreach (byte[] missedMessage in retryQueueForThingsThatFailedToHandle)
    //         {
    //             HandleOpponentDataPacket(missedMessage);
    //         }
    //     }
    //     catch
    //     {
    //         Debug.Log("Error while handling cached messages");
    //     }
    // }

    // private void HandleOpponentDataPacket(byte[] dataPacket)
    // {
    //     try
    //     {
    //         string opponentDataSent = ConvertByteArrayToString(dataPacket);
    //         Debug.Log($"Received P2P payload: {opponentDataSent}");
    //         // TODO: Replace with your own packet handling.
    //     }
    //     catch
    //     {
    //         Debug.Log("Failed to process incoming opponent data packet");
    //     }
    // }

    // private byte[] ConvertStringToByteArray(string stringToConvert)
    // {
    //     return stringToConvert.Length != 0 ? System.Text.Encoding.UTF8.GetBytes(stringToConvert) : System.Text.Encoding.UTF8.GetBytes(string.Empty);
    // }

    // private string ConvertByteArrayToString(byte[] byteArrayToConvert)
    // {
    //     return System.Text.Encoding.UTF8.GetString(byteArrayToConvert);
    // }
}
