using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;


class BuffclipServer : NetworkManager
{
    private readonly Dictionary<IPAddress, IPAddress?> ListenAddresses = new Dictionary<IPAddress, IPAddress?>(); // Save ip and mask
    private readonly List<ClientConnection> clients = new(); // Mantain a client list
    private readonly object clientsLock = new object();      // Lock for securely managing threads
    private byte nextNodeId = 2;
    private readonly object nodeIdLock = new object();

    public override bool IsConnected
    {
        get
        {
            lock (clientsLock) {
                return clients.Count > 0;
            }
        }
    }

    public BuffclipServer(Dictionary<IPAddress, IPAddress?> ListenAddresses, int port)
    {
        this.ListenAddresses = ListenAddresses;
        this.port            = port;
        this.node_id         = 1; // node_id 1 is the server
    }

    private void RunAcceptLoop(TcpListener listener)
    {
        while (true)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                byte assignedId;
                lock (nodeIdLock)
                {
                    assignedId = nextNodeId;
                    nextNodeId++;
                }
                ClientConnection clientConnection = new ClientConnection(assignedId, client);
                Console.WriteLine($"[+] Client connected: {client.Client.RemoteEndPoint} (Assigned Node ID: {assignedId})");

                // Notify the client of its assigned node_id right away
                Packet assignPacket = new Packet(this.node_id, Opcode.AssignNodeId, assignedId, "");
                clientConnection.SendPacket(assignPacket);

                Thread thread = new Thread(() => HandleClient(clientConnection));
                thread.IsBackground = true;
                thread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error accepting client: {ex.Message}");
            }
        }
    }

    public void Start()
    {
        List<TcpListener> listeners = new List<TcpListener>();
        foreach (var ipAddress in this.ListenAddresses.Keys)
        {
                try
                {
                    TcpListener listener = new TcpListener(ipAddress, this.port);
                    listener.Start();
                    listeners.Add(listener);
                    Console.WriteLine($"[+] Server listening on {ipAddress}:{this.port}...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[-] Error starting server on {ipAddress.ToString()}:{this.port}: {ex.Message}");
                }
        }

        if (listeners.Count == 0)
        {
            Console.WriteLine("[-] No valid listening interfaces could be started. Server exiting.");
            return;
        }

        // Start background accept loops for all but the last listener
        for (int i = 0; i < listeners.Count - 1; i++)
        {
            var listener = listeners[i];
            Thread listenThread = new Thread(() => RunAcceptLoop(listener));
            listenThread.IsBackground = true;
            listenThread.Start();
        }

        // Run the last listener's loop directly on this thread to block it naturally
        RunAcceptLoop(listeners[listeners.Count - 1]);
    }

    private void HandleClient(ClientConnection conn)
    {
        Console.WriteLine($"[+] Handling client {conn.node_id}...");

        lock (clientsLock) {
            clients.Add(conn);
        }

        try {
            while (true) {
                Packet packet = this.ReceivePacket(conn.NetStream);

                switch (packet.opcode) {
                    case Opcode.FullSync:
                        this.HandleFullSyncRequest(conn);
                        break;

                    case Opcode.UpdateBuffer:
                        this.HandleUpdateBuffer(packet, conn);
                        break;

                    default:
                        Console.WriteLine($"[-] Unknown opcode: {packet.opcode} from client {conn.node_id}");
                        break;
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"[-] Client {conn.node_id} disconnected: {ex.Message}");
        } finally {
            lock (clientsLock) {
                clients.Remove(conn);
            }
            conn.NetStream?.Close();
            conn.client?.Close();
        }
    }

    public void HandleFullSyncRequest(ClientConnection conn)
    {
        Console.WriteLine($"Recibida la FullSyncRequest de cliente {conn.node_id}, preparando paquetes...");
        
        for (byte idx = 0; idx < Globals.BuffersManager.NumberOfBuffers; idx++) {
            Packet packet = new Packet(this.node_id, Opcode.UpdateBuffer, (byte)(idx + 1), Globals.BuffersManager.buffers[idx]);
            conn.SendPacket(packet);
        }
        Console.WriteLine("Paquetes enviados");
    }

    public override void SendUpdateBuffer(byte id_buf)
    {
        Console.WriteLine($"[i] Host F1/F3 copy: SendUpdateBuffer({id_buf}) called.");
        // Broadcast local host update to all clients
        Packet packet = new Packet(this.node_id, Opcode.UpdateBuffer, id_buf, Globals.BuffersManager.GetBuf(id_buf));
        
        List<ClientConnection> clientsCopy;
        lock (clientsLock) {
            clientsCopy = new List<ClientConnection>(clients);
        }

        Console.WriteLine($"[i] Broadcasting buffer {id_buf} update to {clientsCopy.Count} clients.");
        foreach (var clientConn in clientsCopy) {
            Console.WriteLine($"[i] Sending update to client {clientConn.node_id}...");
            try {
                clientConn.SendPacket(packet);
            } catch (Exception ex) {
                Console.WriteLine($"[-] Error sending to client {clientConn.node_id}: {ex.Message}");
            }
        }
    }

    public void HandleUpdateBuffer(Packet packet, ClientConnection sender)
    {
        // Update local buffer
        Globals.BuffersManager.SetBuf(packet.id_buf, packet.content);

        // Forward to all other clients
        List<ClientConnection> clientsCopy;
        lock (clientsLock) {
            clientsCopy = new List<ClientConnection>(clients);
        }

        foreach (var clientConn in clientsCopy) {
            if (clientConn != sender) {
                // Update packet's node_id to the sender's node_id assigned by the server
                packet.node_id = sender.node_id;
                clientConn.SendPacket(packet);
            }
        }
    }


    // Starts udp listener that handles Discover Packets
    public void StartDiscoveryListener()
    {
        UdpClient udp = new UdpClient(this.port);

        while (true) {
            
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udp.Receive(ref remote);
            Packet packet = Packet.FromBytes(data);

            if (packet.opcode != Opcode.Discover)
                continue;
            
            string? responseIp = null;
           
            foreach (var (listenIp, mask) in this.ListenAddresses)
            {
                
                if (listenIp.Equals(IPAddress.Any))
                {
                    responseIp = GetLocalAddressFor(remote.Address)?.ToString();
                    break;
                }

                if (IsSameSubnet(listenIp, remote.Address, mask))
                {
                    responseIp = listenIp.ToString();
                    break;
                }
            }

            // Client is not in any subnet we are listening on.
            // Ignore the packet.
            if (responseIp == null)
                continue;

            Packet responsePacket = new Packet(0, Opcode.DiscoverResponse, 0, responseIp);
            byte[] packetBytes = responsePacket.ToBytes();
            udp.Send(packetBytes, packetBytes.Length, remote);
        }
    }


    public static IPAddress? GetSubnetMask(IPAddress ip) 
    {
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (addr.Address.Equals(ip))
                    return addr.IPv4Mask;
            }
        }

        return null;
    }

    private static bool IsSameSubnet(IPAddress a, IPAddress b, IPAddress? mask)
    {
        if (mask == null) return false;

        byte[] aa = a.GetAddressBytes();
        byte[] bb = b.GetAddressBytes();
        byte[] mm = mask.GetAddressBytes();

        for (int i = 0; i < aa.Length; i++)
        {
            if ((aa[i] & mm[i]) != (bb[i] & mm[i]))
                return false;
        }

        return true;
    }

    private static IPAddress? GetLocalAddressFor(IPAddress remoteIp)
    {
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

                if (IsSameSubnet(addr.Address, remoteIp, addr.IPv4Mask))
                    return addr.Address;
            }
        }

        return null;
    }
}
