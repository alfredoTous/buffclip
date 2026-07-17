using System.Net.Sockets;

class BuffclipClient : NetworkManager
{

    private string ip = "";

    public BuffclipClient(string ip, int port)
    {
        this.ip      = ip;
        this.port    = port;
        this.node_id = 2; // 2 for now
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
    }

    private void SendFullSyncRequest()
    {
        Packet packet = new Packet(this.node_id, Opcode.FullSync);
        this.SendPacket(packet);
    }

    private void GetFullSyncResponse()
    {
        for (int idx=0; idx<Globals.BuffersManager.NumberOfBuffers; idx++)
        {
            Packet packet = ReceivePacket();
            Globals.BuffersManager.SetBuf(idx, packet.content);
            Console.WriteLine($"[+] Buf #{idx}: {packet.content}");
        }
    }

    private void SyncBuffers()
    {
        SendFullSyncRequest();
        GetFullSyncResponse();
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
                Packet packet = ReceivePacket();

                switch (packet.opcode)
                {
                    case Opcode.UpdateBuffer:
                        this.HandleUpdateBuffer(packet);
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

}


