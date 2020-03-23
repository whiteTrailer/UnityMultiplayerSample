using UnityEngine;
using UnityEngine.Assertions;

using Unity.Collections;
using Unity.Networking.Transport;

using System.Collections.Generic;



public class NetworkServer : MonoBehaviour
{
    public UdpNetworkDriver m_Driver;
    private NativeList<NetworkConnection> m_Connections;
    private List<int> clientIds;

    private int nextId = 1;

    Dictionary<int, Vector3> receivedCubePositions;
    

    void Start()
    {
        m_Driver = new UdpNetworkDriver(new INetworkParameter[0]);
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = 12345;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port 9000");
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        clientIds = new List<int>(16);

        receivedCubePositions = new Dictionary<int, Vector3>();
    }



    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    bool needSend = false;

    void Update ()
    {
        if (needSend)
        {
            needSend = false;
            SendPositions();
        }
        m_Driver.ScheduleUpdate().Complete();

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                if (receivedCubePositions.ContainsKey(clientIds[i]))
                {
                    receivedCubePositions.Remove(clientIds[i]);
                }
                clientIds.RemoveAtSwapBack(i);                
                --i;
            }
        }

        //SendPositions();
        needSend = true;

        // AcceptNewConnections
        NetworkConnection c;
        while ((c = m_Driver.Accept()) != default(NetworkConnection))
        {
            m_Connections.Add(c);
            clientIds.Add(nextId);
            Debug.Log("Accepted a connection");
            receivedCubePositions.Add(nextId, new Vector3());
            nextId++;
        }

        DataStreamReader stream;
        Debug.Log("Cons_Cnt: " + m_Connections.Length);
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
                Assert.IsTrue(true);

            NetworkEvent.Type cmd;
            while ((cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream)) !=
                   NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    //читаем данные и актуализируем позиции кубов
                    var readerCtx = default(DataStreamReader.Context);
                    float x = stream.ReadFloat(ref readerCtx);
                    float y = stream.ReadFloat(ref readerCtx);
                    float z = stream.ReadFloat(ref readerCtx);

                    receivedCubePositions[clientIds[i]] = new Vector3(x, y, z);

                    Debug.Log("Received: " + receivedCubePositions[clientIds[i]]);
                    Debug.Log("Cons_Cnt: " + m_Connections.Length);
                    //var readerCtx = default(DataStreamReader.Context);
                    //uint number = stream.ReadUInt(ref readerCtx);

                    //Debug.Log("Got " + number + " from the Client adding + 2 to it.");
                    //number +=2;

                    //using (var writer = new DataStreamWriter(4, Allocator.Temp))
                    //{
                    //    writer.Write(number);
                    //    m_Driver.Send(NetworkPipeline.Null, m_Connections[i], writer);
                    //}
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected from server");
                    m_Connections[i] = default(NetworkConnection);
                }
            }
        }
    }

    private void SendPositions()
    {
        for (int i = 0; i < m_Connections.Length; i++)
        {
            using (var writer = new DataStreamWriter())
            {
                foreach (var cube in receivedCubePositions)
                {
                    Debug.Log(cube);
                    if (cube.Key != clientIds[i])
                    {
                        writer.Write(cube.Key);
                        writer.Write(cube.Value.x);
                        writer.Write(cube.Value.y);
                        writer.Write(cube.Value.z);
                    }
                }
                m_Driver.Send(NetworkPipeline.Null, m_Connections[i], writer);
            }
        }
    }
}