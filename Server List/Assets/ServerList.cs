﻿using UnityEngine;
using UnityEngine.Networking;

using System.Collections.Generic;
using System;
using System.Text;
using System.IO;

public class ServerList : Essentials {
    #region
    public Dictionary<string, MasterServer> serverList;

    public void Add(MasterServer mS) {
        serverList.Add(mS.ip, mS);
    }

    #region Network
    #region Network Variables
    // Configuration Channels
    byte reliableChanId;

    // Sockect Configurations
    int socketId;
    int socketPort = 20000;

    // Communication
    int connectionId;
    #endregion

    void Start()
    {
        LoadPassword();

        // Initializing the Transport Layer with no arguments (default settings)
        NetworkTransport.Init();

        // Configuration Channels
        ConnectionConfig config = new ConnectionConfig();
        reliableChanId = config.AddChannel(QosType.Reliable);

        // Max Connections
        int maxConnections = 10;
        HostTopology topology = new HostTopology(config, maxConnections);

        // Socket Configurations
        socketId = NetworkTransport.AddHost(topology, socketPort);
        print("Socket open on port " + socketPort + ": ID #" + socketId);

        // Communication
        connectionId = Connect("127.0.0.1", socketPort, socketId);
    }

    void Update()
    {
        Listen();
    }

    /// <summary>
    /// Connect to... yeah.. It's self-explanatory, dammit.
    /// </summary>
    /// <param name="ip">The ip address to connect to.</param>
    /// <param name="port">The port that is being hosted on.</param>
    /// <param name="id">The host connection ID.</param>
    int Connect(string ip, int port, int id = 0)
    {
        byte error;

        int newConnectionId = NetworkTransport.Connect(id, ip, port, 0, out error);
        if ((NetworkError)error != NetworkError.Ok)
        {
            print("Failed to connect because:" + (NetworkError)error);
        }
        else {
            print("Connected to server (" + ip + ":" + port + "): Connection ID #" + newConnectionId);
        }

        return newConnectionId;
    }

    /// <summary>
    /// Receives data.
    /// </summary>
    void Listen()
    {
        int recHostId;
        int recConnectionId;
        int recChannelId;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error;
        NetworkEventType recData = NetworkTransport.Receive(
            out recHostId,
            out recConnectionId,
            out recChannelId,
            recBuffer,
            bufferSize,
            out dataSize,
            out error
        );

        switch (recData)
        {
            case NetworkEventType.DataEvent:
                // Find a way to tell if it was serialized or not.
                ///Stream stream = new MemoryStream(recBuffer);
                ///BinaryFormatter formatter = new BinaryFormatter();
                ///string message = formatter.Deserialize(stream) as string;
                string message = ByteArrayToString(recBuffer, Encoding.UTF8).Substring(0, dataSize);
                print("Message received: " + message);

                byte code = Convert.ToByte(message.Substring(0, message.IndexOf(' ') + 1));
                PerformAction(code, message.Substring(message.IndexOf(' ') + 1).TrimEnd(new char[] { '\r', '\n' }), recConnectionId);
                break;

            case NetworkEventType.ConnectEvent:
                if (connectionId == recConnectionId)
                {
                    print("Self-connection approved.");
                }
                else {
                    print("Remote connection incoming.");
                }
                break;

            case NetworkEventType.DisconnectEvent:
                if (connectionId == recConnectionId)
                {
                    print("Self-connection failed: " + (NetworkError)error);
                }
                else {
                    print("Remote connection closed.");
                }
                break;

            case NetworkEventType.Nothing:
                //print("Nothing received.");
                break;

            case NetworkEventType.BroadcastEvent:
                print("Broadcast discovery event received.");
                break;
        }
    }
    #endregion

    #region Actions
    #region Action Variables
    [Flags]
    public enum ListData : byte
    {
        ip = 0x00,
        curPlayers = 0x01,
        maxPlayers = 0x02,
        servers = 0x03

    }
    #endregion

    void PerformAction(byte code, string msg, int id)
    {
        // Unsure if this cast will work.
        switch ((Actions)code)
        {
            case Actions.Debug:
                debug("Debugging socket: " + msg);
                break;

            case Actions.Auth:
                Authenticate(msg, id);
                break;
        }
    }
    #endregion

    #region Security & Authentication
    #region Security & Authentication Variables
    public string password;
    #endregion

    bool LoadPassword()
    {
        string rawPath = Application.dataPath + "/auth/";
        string path = Application.dataPath + "/auth/keyphrase.passwd";

        if (!Directory.Exists(rawPath))
        {
            Directory.CreateDirectory(rawPath);
        }
        else {
            if (File.Exists(path))
            {
                password = File.ReadAllText(path);
                return true;
            }
        }

        password = CreatePassword(256);
        File.WriteAllText(path, password);
        return false;
    }

    string CreatePassword(int length)
    {
        const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()_-+=[{}]|:;<>,.?";
        StringBuilder pass = new StringBuilder();

        while (0 < length--)
        {
            pass.Append(valid[UnityEngine.Random.Range(0, valid.Length)]);
        }

        return pass.ToString();
    }

    void Authenticate(string msg, int id)
    {
        if (password == msg)
        {
            debug("Server has been authenticated.");
        }
        else {
            debug("Server has failed authentication. (10 attempts left before added to blacklist)"); // blacklist can be edited easily via text file so the user won't have to go through a lot of trouble to remove a mistaken blacklist

            byte error;
            NetworkTransport.Disconnect(socketId, id, out error);

            if ((NetworkError)error != NetworkError.Ok)
            {
                debug("Failed to disconnect remote connection because:" + (NetworkError)error);
            }
        }
    }
    #endregion
}

public class MasterServer {
    public int servers;
    public string ip;
    public int curPlayers;
    public int maxPlayers;

    public MasterServer(string ip) {
        this.ip = ip;
    }
}