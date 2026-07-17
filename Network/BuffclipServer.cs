using System.Net.Sockets;
using System.Net;


class BuffclipServer : NetworkManager
{

    private string listenAddress = "";

    public BuffclipServer(string listenAddress, int port)
    {
        this.listenAddress = listenAddress;
        this.port          = port;
        this.node_id       = 1;     // 1 for now
    }

    public void StartServer()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, this.port);
        listener.Start();
        Console.WriteLine($"[+] Server listening on {IPAddress.Any}:{this.port}...");

        while (true) {
            TcpClient client = listener.AcceptTcpClient();
            Console.WriteLine($"[+] Client connected: {client.Client.RemoteEndPoint}");
            HandleClient(client);
        }
    }

    private void HandleClient(TcpClient client)
    {
        Console.WriteLine("[+] Handling client...");

        this.client = client;
        this.NetStream = client.GetStream();

        try {
            while (true) {
                Packet packet = this.ReceivePacket();

                switch (packet.opcode) {
                    case Opcode.FullSync:
                        HandleFullSyncRequest();
                        break;

                    case Opcode.UpdateBuffer:
                        //HandleUpdateBuffer(packet);
                        break;

                    default:
                        Console.WriteLine($"[-] Unknow opcode: {packet.opcode}");
                        break;
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"[-] Client disconnected: {ex.Message}");
        } finally {
            this.NetStream?.Close();
            client.Close();

            this.NetStream = null;
            this.client = null;
        }
    }

    public void HandleFullSyncRequest()
    {
        Console.WriteLine("Recibida la FullSyncRequest, preparando paquetes...");
        
        for (byte idx=0; idx<Globals.BuffersManager.NumberOfBuffers; idx++) {
            Packet packet = new Packet(this.node_id, Opcode.UpdateBuffer, idx, Globals.BuffersManager.buffers[idx]);
            SendPacket(packet);
        }
        Console.WriteLine("Paquetes enviados");
    }


}
