using System;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

// Code for SteamNetworkingSockets which replaced SteamNetworking P2P code in the tutorial
// Socket server lives on the host; all players connect via ConnectionManager.
public class SteamSocketManager : SocketManager
{
    public override void OnConnecting(Connection connection, ConnectionInfo data)
    {
        base.OnConnecting(connection, data);
        Debug.Log("SocketManager OnConnecting");
    }

    public override void OnConnected(Connection connection, ConnectionInfo data)
    {
        base.OnConnected(connection, data);
        Debug.Log("New player connecting");
    }

    public override void OnDisconnected(Connection connection, ConnectionInfo data)
    {
        base.OnDisconnected(connection, data);
        Debug.Log("Player disconnected");
    }

    public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        // Socket server received message, forward message to all other members
        SteamManager.Instance.RelaySocketMessageReceived(data, size, connection.Id);
        Debug.Log("Socket message received");
    }
}

// Connection manager that enables all players to connect to the socket server
public class SteamConnectionManager : ConnectionManager
{
    public override void OnConnected(ConnectionInfo info)
    {
        base.OnConnected(info);
        Debug.Log("ConnectionOnConnected");
    }

    public override void OnConnecting(ConnectionInfo info)
    {
        base.OnConnecting(info);
        Debug.Log("ConnectionOnConnecting");
    }

    public override void OnDisconnected(ConnectionInfo info)
    {
        base.OnDisconnected(info);
        Debug.Log("ConnectionOnDisconnected");
    }

    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        // Message received from socket server, delegate to SteamManager for handling
        SteamManager.Instance.ProcessMessageFromSocketServer(data, size);
        Debug.Log("Connection Got A Message");
    }
}
