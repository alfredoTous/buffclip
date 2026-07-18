using System.Net.Sockets;

class ClientConnection
{
    public byte node_id { get; set; }
    public TcpClient client { get; set; }
    public NetworkStream NetStream { get; set; }
    private readonly object sendLock = new object();

    public ClientConnection(byte node_id, TcpClient client)
    {
        this.node_id   = node_id;
        this.client    = client;
        this.NetStream = client.GetStream();
    }

    public void SendPacket(Packet packet)
    {
        try {
            byte[] packetBytes = packet.ToBytes();
            byte[] lenBytes    = BitConverter.GetBytes(packetBytes.Length);

            lock (sendLock) {
                this.NetStream.Write(lenBytes, 0, lenBytes.Length);
                this.NetStream.Write(packetBytes, 0, packetBytes.Length);
                this.NetStream.Flush();
            }
        } catch (Exception ex) {
            Console.WriteLine($"[-] Error in ClientConnection.SendPacket (Client {node_id}): {ex.Message}");
        }
    }
}
