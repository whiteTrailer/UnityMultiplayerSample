using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class NetworkClient : MonoBehaviour
{
    [SerializeField] float timeToSend = 1/30.0f;
    float curTimeToSend;

    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP; //127.0.0.1 - local // 54.167.55.163 - global
    public ushort serverPort;

    public GameObject cube;
    public GameObject cubeInstance;

    bool receivedId = false;
    string playerId = "";

    Dictionary<string, GameObject> connectedPlayers;
    
    //Create driver, set connection to default
    void Start ()
    {
        //Screen.fullScreen = false;
        Screen.SetResolution(1280, 720, false);

        connectedPlayers = new Dictionary<string, GameObject>();

        cubeInstance.GetComponent<MeshRenderer>().material.color = new Color(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
        cubeInstance.transform.position = new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-2f, 2f));

        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        receivedId = false;

        var endpoint = NetworkEndPoint.Parse(serverIP, serverPort);
        //Connect driver 
        m_Connection = m_Driver.Connect(endpoint);

    }
    
    
    void SendToServer(string message){
        //Create writer 
        var writer = m_Driver.BeginSend(m_Connection);
        //Create array of bytes 
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        //Write bytes to writer 
        writer.WriteBytes(bytes);
        //Send message (writer)
        m_Driver.EndSend(writer);
    }


    public void Connect()
    {   //If connection is default 
        //if (!m_Connection.IsCreated)
        //{   //Create server address 
        //    var endpoint = NetworkEndPoint.Parse(serverIP, serverPort);
        //    //Connect driver 
        //    m_Connection = m_Driver.Connect(endpoint);
        //    //Instantiate players cube
        //    //Instantiate(cubeInstance);
        //}
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");

        // Example to send a handshake message:
        //Create an instance of handshake message 
        HandshakeMsg m = new HandshakeMsg();
        //Send handshake message as a json
        SendToServer(JsonUtility.ToJson(m));
    }

    void SendData()
    {   //If its time to send 
        if (curTimeToSend >= timeToSend && receivedId)
        {   //Create an instance of pu message 
            PlayerUpdateMsg msg = new PlayerUpdateMsg();
            //Save cube pos to pu message 
            msg.player.cubPos = cubeInstance.transform.position;
            //Save cube color to pu message
            msg.player.cubeColor = cubeInstance.GetComponent<MeshRenderer>().material.color;
            msg.player.id = playerId;
            //Send pu message as a json
            SendToServer(JsonUtility.ToJson(msg));
            //Reset timer 
            curTimeToSend = 0;
        }
        //Update timer 
        curTimeToSend += Time.deltaTime;
    }

    void OnData(DataStreamReader stream){
        //Create an array of bytes 
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        //Read bytes
        stream.ReadBytes(bytes);
        //Convert data to string 
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        //Get a command from json 
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
            HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
            Debug.Log("Handshake message received!");
            receivedId = true;
            playerId = hsMsg.player.id;

            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            Debug.Log("Player update message received!");
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received!");
            ParseServerUpdateMsg(suMsg.players);
            break;
            default:
            Debug.Log("Unrecognized message received!");
            break;
        }
    }

    private void ParseServerUpdateMsg(List<NetworkObjects.NetworkPlayer> players)
    {
        players.RemoveAll(p => p.id == playerId);

        List<string> keysToRemove = new List<string>();

        foreach (var key in connectedPlayers.Keys)
            if (players.FindIndex(p => p.id == key) < 0)
                keysToRemove.Add(key);

        foreach (var key in keysToRemove)
        {
            Destroy(connectedPlayers[key]);
            connectedPlayers.Remove(key);
        }

        foreach (var player in players)
        {
            if (!connectedPlayers.ContainsKey(player.id))
            {
                var cubeInst = Instantiate(cube);
                connectedPlayers.Add(player.id, cubeInst);
            }
            connectedPlayers[player.id].transform.position = player.cubPos;
            connectedPlayers[player.id].GetComponent<MeshRenderer>().material.color = player.cubeColor;
        }
    }

    public void Disconnect()
    {
        //m_Driver.Dispose();
        //m_Driver = NetworkDriver.Create();
        //m_Connection = default(NetworkConnection);
        //receivedId = false;
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        //Set connection to default
        m_Connection = default(NetworkConnection);
    }

    //Disconnect client, dispose driver 
    public void OnDestroy()
    {
        m_Connection.Disconnect(m_Driver);
        m_Driver.Dispose();
    }   
    void Update()
    {
        //Ready to process updates 
        m_Driver.ScheduleUpdate().Complete();

        //If no connection - return 
        if (!m_Connection.IsCreated)
        {
            return;
        }

        ReadIncomingMessages();

        //Send data to server every second 
        SendData();
    }

    private void ReadIncomingMessages()
    {
        //Stream reader 
        DataStreamReader stream;

        //Type of network event
        NetworkEvent.Type cmd;
        //Get an event 
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        //While network type excists 
        while (cmd != NetworkEvent.Type.Empty)
        {
            //If type connect - connect
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            //If type data - process commands 
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            //If type disconnect - disconnect 
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            //Get a new event 
            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }
}