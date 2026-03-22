using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class AutoStartNetcode : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    [Header("LAN discovery")]
    [SerializeField] private ushort gamePort = 7777;
    [SerializeField] private ushort discoveryPort = 47777;
    [SerializeField] private float discoverTime = 0.8f;

    private void Awake()
    {
        if (networkManager == null)
            networkManager = FindObjectOfType<NetworkManager>();

        if (networkManager != null && transport == null)
            transport = networkManager.GetComponent<UnityTransport>();
    }

    private void Start()
    {
        StartCoroutine(AutoJoinOrHost());
    }

    private IEnumerator AutoJoinOrHost()
    {
        yield return null;

        if (networkManager == null)
        {
            Debug.LogError("No NetworkManager found in the scene.");
            yield break;
        }

        if (transport == null)
        {
            Debug.LogError("No UnityTransport found on the NetworkManager.");
            yield break;
        }

        UdpClient probe = new UdpClient(0);
        probe.EnableBroadcast = true;

        byte[] discover = Encoding.UTF8.GetBytes("DISCOVER_JOJO");
        probe.Send(discover, discover.Length, new IPEndPoint(IPAddress.Broadcast, discoveryPort));

        float end = Time.realtimeSinceStartup + discoverTime;

        while (Time.realtimeSinceStartup < end)
        {
            if (probe.Available > 0)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                string reply = Encoding.UTF8.GetString(probe.Receive(ref remote));

                if (reply == "JOJO_HOST")
                {
                    probe.Close();
                    transport.SetConnectionData(remote.Address.ToString(), gamePort);
                    networkManager.StartClient();
                    yield break;
                }
            }

            yield return null;
        }

        probe.Close();

        transport.SetConnectionData("0.0.0.0", gamePort);
        networkManager.StartHost();
        StartCoroutine(HostDiscoveryLoop());
    }

    private IEnumerator HostDiscoveryLoop()
    {
        UdpClient socket = new UdpClient(discoveryPort);
        socket.EnableBroadcast = true;

        while (networkManager != null && networkManager.IsServer)
        {
            while (socket.Available > 0)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                socket.Receive(ref remote);

                byte[] reply = Encoding.UTF8.GetBytes("JOJO_HOST");
                socket.Send(reply, reply.Length, remote);
            }

            yield return null;
        }

        socket.Close();
    }
}