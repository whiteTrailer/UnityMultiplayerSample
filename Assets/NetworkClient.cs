using UnityEngine;

using Unity.Collections;
using Unity.Networking.Transport;

using UnityEngine.Networking;
using System.IO;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;

public class NetworkClient : MonoBehaviour
{
    public UdpNetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public bool m_Done;

    public GameObject cube;
    GameObject cubeInstance;

    Dictionary<int, GameObject> connectedPairDictionary;
    

    void Start ()
    {
        if (Screen.fullScreen)
        {
            Screen.SetResolution(1300, 800, false);
        }
        m_Driver = new UdpNetworkDriver(new INetworkParameter[0]);
        m_Connection = default(NetworkConnection);

        //var endpoint = NetworkEndPoint.Parse("52.15.219.197",12345);

        var endpoint = NetworkEndPoint.LoopbackIpv4;
        endpoint.Port = 12345;
        m_Connection = m_Driver.Connect(endpoint);


        connectedPairDictionary = new Dictionary<int, GameObject>();
    }

    public void OnDestroy()
    {
        m_Connection.Disconnect(m_Driver);
        m_Driver.Dispose();
    }
   

    bool needSend = false;

    void Update()
    {
        if (needSend)
        {
            needSend = false;
            SendPosition();
        }

        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            if (!m_Done)
                Debug.Log("Something went wrong during connect");
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        
        while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) !=
               NetworkEvent.Type.Empty || needSend)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                cubeInstance = Instantiate(cube);
                
                needSend = true;
                //SendPosition();
                
                //Здесь инстанцируем куб
                //отправляем его позицию

                //Debug.Log("We are now connected to the server");
                //Instantiate(cube);

                //var value = 1;
                //using (var writer = new DataStreamWriter(4, Allocator.Temp))
                //{
                //    writer.Write(value);
                //    m_Connection.Send(m_Driver, writer);
                //}
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                needSend = true;

                var positionsFromServer = new Dictionary<int, Vector3>();
                
                int cnt = stream.Length / 16;                

                foreach (var pair in positionsFromServer)
                {
                    if (!connectedPairDictionary.ContainsKey(pair.Key))
                        connectedPairDictionary.Add(pair.Key, Instantiate(cube));
                    connectedPairDictionary[pair.Key].transform.position = pair.Value;
                }

                List<int> desIds = new List<int>();
                foreach (var pair in connectedPairDictionary)
                {
                    if (!positionsFromServer.ContainsKey(pair.Key))
                    {
                        desIds.Add(pair.Key);
                    }
                }

                foreach (var id in desIds)
                {
                    Destroy(connectedPairDictionary[id]);
                    connectedPairDictionary.Remove(id);
                }

                //читаем информацию, интанцируем-удаляем-двигаем кубы

                //var readerCtx = default(DataStreamReader.Context);
                //uint value = stream.ReadUInt(ref readerCtx);
                //Debug.Log("Got the value = " + value + " back from the server");
                //m_Done = true;
                //m_Connection = default(NetworkConnection);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {

                //Удаляем все кубы
                foreach (var cube in connectedPairDictionary)
                {
                    Destroy(cube.Value);
                }
                connectedPairDictionary.Clear();

                //Debug.Log("Client got disconnected from server");
                //Destroy(cube);
                
                m_Connection = default(NetworkConnection);
            }
        }
    }

    private void SendPosition()
    {
        using (var writer = new DataStreamWriter())
        {
            writer.Write(cubeInstance.transform.position.x);
            writer.Write(cubeInstance.transform.position.y);
            writer.Write(cubeInstance.transform.position.z);
            m_Connection.Send(m_Driver, writer);
        }
    }
}