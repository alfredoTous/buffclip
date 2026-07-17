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

    public void Connect()
    {
        this.client = new TcpClient();
        Console.WriteLine($"[i] Connecting to {ip}:{this.port}...");
        this.client.Connect(this.ip, this.port);

        this.NetStream = this.client.GetStream();
    }

    public void SendFullSyncRequest()
    {
        Packet packet = new Packet(this.node_id, Opcode.FullSync);
        this.SendPacket(packet);
    }

    public void GetFullSyncResponse()
    {
        for (int idx=0; idx<Globals.BuffersManager.NumberOfBuffers; idx++)
        {
            Packet packet = ReceivePacket();
            Globals.BuffersManager.SetBuf(idx, packet.content);
            Console.WriteLine($"[+] Buf #{idx}: {packet.content}");
        }
    }

}


