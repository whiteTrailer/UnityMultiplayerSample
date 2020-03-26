using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using System;
using System.Text;
using System.Collections.Generic;

public class NetworkServer : MonoBehaviour
{
    [SerializeField] float timeToSend = 1 / 30.0f;
    float curTimeToSend;

    int connectionId;

    public NetworkDriver m_Driver;
    public ushort serverPort;

    //List of connections
    private NativeList<NetworkConnection> m_Connections;
    private NativeList<int> playersIds;
    
    //Disctionary of players - ids, players data 
    Dictionary<string, NetworkObjects.NetworkPlayer> players;

    void Start ()
    {   //Create driver 
        m_Driver = NetworkDriver.Create();
        //Create server address
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        //If bind is not failed - listen 
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        //Create list of connections 
        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        playersIds = new NativeList<int>(16, Allocator.Persistent);
        //Create dictionary of players ids and data 
        players = new Dictionary<string, NetworkObjects.NetworkPlayer>();
        //Reset timer
        curTimeToSend = timeToSend;
    }

    void SendToClient(string message, NetworkConnection c){
        //Create writer 
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        //Create array of bytes 
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        //Write bytes to writer 
        writer.WriteBytes(bytes);
        //Send writer
        m_Driver.EndSend(writer);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void OnConnect(NetworkConnection c){
        m_Connections.Add(c);
        playersIds.Add(connectionId);
        players.Add(connectionId.ToString(), new NetworkObjects.NetworkPlayer { id = connectionId.ToString() });

        Debug.Log("Accepted a connection of " + connectionId);

        connectionId++;
        
        //foreach(var p in players)
        //{
        //    Debug.Log("Added " + p.Key + ": " + p.Value);
        //}

        //// Example to send a handshake message:
        // HandshakeMsg m = new HandshakeMsg();
        // m.player.id = c.InternalId.ToString();
        // SendToClient(JsonUtility.ToJson(m),c);        
    }

    void OnData(DataStreamReader stream, int i){
        //Create array of bytes 
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        //Read bytes 
        stream.ReadBytes(bytes);
        //Convert to string 
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        //Get a command from json 
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
            HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
            Debug.Log("Handshake message received!");
            SendIdToClient(i);
            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            Debug.Log("Player update message received from " + puMsg.player.id);
            
            //Add player data with key "id" to dictionary of players
            players[puMsg.player.id] = puMsg.player;

            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received! !!!STRANGE!!!");
            break;
            default:
            Debug.Log("SERVER ERROR: Unrecognized message received!");
            break;
        }
    }

    void OnDisconnect(int i){
        Debug.Log("Client disconnected from server");

        players.Remove(playersIds[i].ToString());
        
        m_Connections[i] = default(NetworkConnection);
    }

    void Update ()
    {   //Ready to process events 
        m_Driver.ScheduleUpdate().Complete();

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                playersIds.RemoveAtSwapBack(i);
                --i;
            }
        }
        //Send data to clients 
        SendDataToClients();

        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c != default(NetworkConnection))
        {
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }

        // Read Incoming Messages
        ReadIncomingMessages();
    }

    private void SendIdToClient(int ci)
    {
        HandshakeMsg msg = new HandshakeMsg();
        msg.player.id = playersIds[ci].ToString();
        SendToClient(JsonUtility.ToJson(msg), m_Connections[ci]);
    }

    private void SendDataToClients()
    {   //If there's one or more players 
        if(players.Count > 0)
        {   //And it's time to send 
            if(curTimeToSend >= timeToSend)
            {   //Create server update message 
                ServerUpdateMsg msg = new ServerUpdateMsg();
                //Create list of players data 
                msg.players = new List<NetworkObjects.NetworkPlayer>(players.Values);
                //For each connection send data to client 
                for (int i = 0; i < m_Connections.Length; i++)
                    SendToClient(JsonUtility.ToJson(msg), m_Connections[i]);

                //Reset timer 
                curTimeToSend = 0;
            }
            //Update timer 
            curTimeToSend += Time.deltaTime;
        }
    }

    private void ReadIncomingMessages()
    {   //Create reader 
        DataStreamReader stream;
        //For each connection 
        for (int i = 0; i < m_Connections.Length; i++)
        {   //If there's more than one connection 
            Assert.IsTrue(m_Connections[i].IsCreated);
            //Type of network event 
            NetworkEvent.Type cmd;
            //Get event 
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            //While event is not empty 
            while (cmd != NetworkEvent.Type.Empty)
            {   //If event data - 
                if (cmd == NetworkEvent.Type.Data)
                {   //Process events 
                    OnData(stream, i);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {   //If type disconnect - disconnect 
                    OnDisconnect(i);
                }

                //Get an event 
                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }
    }
}