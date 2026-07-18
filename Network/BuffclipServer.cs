using System.Net.Sockets;
using System.Net;


class BuffclipServer : NetworkManager
{
    private readonly string[] listenAddresses;
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

    public BuffclipServer(string[] listenAddresses, int port)
    {
        this.listenAddresses = listenAddresses;
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
        foreach (var addressStr in listenAddresses)
        {
            string trimmed = addressStr.Trim();
            if (IPAddress.TryParse(trimmed, out IPAddress? ipAddress))
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
                    Console.WriteLine($"[-] Error starting server on {trimmed}:{this.port}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[-] Invalid IP address format: '{trimmed}'");
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
            Packet packet = new Packet(this.node_id, Opcode.UpdateBuffer, idx, Globals.BuffersManager.buffers[idx]);
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
            clientConn.SendPacket(packet);
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
}
