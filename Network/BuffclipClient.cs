using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;

class BuffclipClient : NetworkManager
{

    private string ip = "";

    public BuffclipClient(string ip, int port)
    {
        this.ip      = ip;
        this.port    = port;
        this.node_id = 0; // Will be assigned by the server upon connection
    }

    public void Start()
    {
        this.Connect();
        this.SyncBuffers();
        this.ListenForServerPackets();
    }

    private void Connect()
    {
        this.client = new TcpClient();
        Console.WriteLine($"[i] Connecting to {ip}:{this.port}...");
        this.client.Connect(this.ip, this.port);

        this.NetStream = this.client.GetStream();

        // Receive the node_id assigned by the server
        Packet assignPacket = this.ReceivePacket();
        if (assignPacket.opcode == Opcode.AssignNodeId)
        {
            this.node_id = assignPacket.id_buf;
            Console.WriteLine($"[+] Assigned node_id: {this.node_id}");
        }
        else
        {
            throw new Exception($"[-] Expected AssignNodeId packet, got opcode {assignPacket.opcode}");
        }
    }

    private void SendFullSyncRequest()
    {
        Packet packet = new Packet(this.node_id, Opcode.FullSync);
        this.SendPacket(packet);
    }

    private void GetFullSyncResponse()
    {
        for (int idx=1; idx<Globals.BuffersManager.NumberOfBuffers+1; idx++)
        {
            Packet packet = ReceivePacket();
            Globals.BuffersManager.SetBuf(idx, packet.content);
            Console.WriteLine($"[+] Buf #{idx}: {packet.content}");
        }
    }

    private void SyncBuffers()
    {
        this.SendFullSyncRequest();
        this.GetFullSyncResponse();
    }

    private void HandleUpdateBuffer(Packet packet) 
    {
        Globals.BuffersManager.SetBuf(packet.id_buf, packet.content);
        Console.WriteLine($"[+] Buffer #{packet.id_buf} updated.");
    }

    public void ListenForServerPackets()
    {
        Console.WriteLine("[+] Listening for server packets...");

        try
        {
            while (true)
            {
                Packet packet = this.ReceivePacket();

                switch (packet.opcode)
                {
                    case Opcode.UpdateBuffer:
                        this.HandleUpdateBuffer(packet);
                        break;

                    case Opcode.AssignNodeId:
                        this.node_id = packet.id_buf;
                        Console.WriteLine($"[+] node_id reassigned to: {this.node_id}");
                        break;

                    default:
                        Console.WriteLine($"[-] Unknown opcode: {packet.opcode}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[-] Lost connection to server: {ex.Message}");
        }
        finally
        {
            this.NetStream?.Close();
            this.client?.Close();

            this.NetStream = null;
            this.client = null;
        }
    }

    // UDP broadcast to discover the server IP automatically
    public static string? DiscoverServerViaBroadcast(int port)
    {
        Console.WriteLine($"[i] Searching for server via broadcast on port {port}...");
        using UdpClient udp = new UdpClient();
        udp.EnableBroadcast = true;
        udp.Client.ReceiveTimeout = 300;

        Packet packet = new Packet(0, Opcode.Discover);
        byte[] packetBytes = packet.ToBytes();

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            IPInterfaceProperties props = nic.GetIPProperties();

            foreach (UnicastIPAddressInformation addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (addr.IPv4Mask == null)
                    continue;

                IPAddress broadcast = GetBroadcastAddress(addr.Address, addr.IPv4Mask);

                Console.WriteLine($"{nic.Name} -> {addr.Address} / {addr.IPv4Mask} -> {broadcast}");
                Console.WriteLine($"[i] Sending Discover to {broadcast}");

                udp.Send(packetBytes, packetBytes.Length, new IPEndPoint(broadcast, port));
            }
        }

        DateTime end = DateTime.Now.AddMilliseconds(1500);

        while (DateTime.Now < end)
        {
            try
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

                byte[] response = udp.Receive(ref remote);

                Packet responsePacket = Packet.FromBytes(response);

                if (responsePacket.opcode != Opcode.DiscoverResponse)
                    continue;

                if (string.IsNullOrWhiteSpace(responsePacket.content))
                    continue;

                Console.WriteLine($"[+] Server discovered: {responsePacket.content}");

                return responsePacket.content;
            }
            catch (SocketException)
            {
                // Receive timeout. Keep trying until the global timeout expires.
            }
        }

        return null;
    }

    private static IPAddress GetBroadcastAddress(IPAddress ip, IPAddress mask)
    {
        byte[] ipBytes   = ip.GetAddressBytes();
        byte[] maskBytes = mask.GetAddressBytes();

        byte[] broadcast = new byte[4];

        for (int i = 0; i < 4; i++)
            broadcast[i] = (byte)(ipBytes[i] | (~maskBytes[i]));

        return new IPAddress(broadcast);
    }

}


