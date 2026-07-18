using System.Net.Sockets;
using System.Net;


class BuffclipServer : NetworkManager
{
    private string listenAddress = "";
    private readonly List<ClientConnection> clients = new(); // Mantain a client list
    private readonly object clientsLock = new object();      // Lock for securely managing threads

    public override bool IsConnected
    {
        get
        {
            lock (clientsLock) {
                return clients.Count > 0;
            }
        }
    }

    public BuffclipServer(string listenAddress, int port)
    {
        this.listenAddress = listenAddress;
        this.port          = port;
        this.node_id       = 1; // node_id 1 is the server
    }

    public void Start()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, this.port);
        listener.Start();
        Console.WriteLine($"[+] Server listening on {IPAddress.Any}:{this.port}...");

        byte next_node_id = 2;
        while (true) {
            try {
                TcpClient client = listener.AcceptTcpClient();
                ClientConnection clientConnection = new ClientConnection(next_node_id, client);
                Console.WriteLine($"[+] Client connected: {client.Client.RemoteEndPoint} (Assigned Node ID: {next_node_id})");
                next_node_id++;

                Thread thread = new Thread(() => HandleClient(clientConnection));
                thread.IsBackground = true;
                thread.Start();
            } catch (Exception ex) {
                Console.WriteLine($"[-] Error accepting client: {ex.Message}");
            }
        }
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
