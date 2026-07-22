using System.Text;
using System.Net.Sockets;


abstract class NetworkManager
{
    protected byte node_id;
    protected int port;
    protected TcpClient? client;
    protected NetworkStream? NetStream;
    public virtual bool IsConnected => this.NetStream != null;


    public virtual void SendUpdateBuffer(byte id_buf) {
        Console.WriteLine("[+] Enviando paquete");
        Packet packet = new Packet(this.node_id, Opcode.UpdateBuffer, id_buf, Globals.BuffersManager.GetBuf(id_buf));
        try {
            SendPacket(packet);
        } catch (Exception ex) {
            Console.WriteLine($"[-] Failed to send update: {ex.Message}");
        }
    }

    protected void SendPacket(Packet packet)
    {
        if (this.NetStream == null)
            return;

        byte[] packetBytes = packet.ToBytes();
        byte[] lenBytes    = BitConverter.GetBytes(packetBytes.Length);

        this.NetStream.Write(lenBytes, 0, lenBytes.Length); // Send a header for packet total len
        this.NetStream.Write(packetBytes, 0, packetBytes.Length);

    }

    protected Packet ReceivePacket(NetworkStream? stream = null)
    {
        NetworkStream netStream = stream ?? this.NetStream ?? throw new Exception("[-] Not connected");

        byte[] lenBytes = new byte[sizeof(int)];
        ReadExact(netStream, lenBytes, sizeof(int)); // Read packet total len
        
        int packetLen = BitConverter.ToInt32(lenBytes);
        byte[] packetBytes = new byte[packetLen];
        ReadExact(netStream, packetBytes, packetLen);

        return Packet.FromBytes(packetBytes);
    }

    private void ReadExact(NetworkStream netStream, byte[] buffer, int len)
    {
        int totalRead = 0;

        while (totalRead < len) {
            int bytesRead = netStream.Read(buffer, totalRead, len-totalRead);
            
            if (bytesRead == 0)
                throw new Exception("Connection closed");

            totalRead+=bytesRead;
        }
    }
}

enum Opcode : byte
{
    UpdateBuffer     = 1, // Update buffer
    FullSync         = 2, // When a new machine is connected, sync buffers
    Discover         = 3, // Used by client for automatic discover of buffclip server
    DiscoverResponse = 4, // Used by server to respond to a Discover packet
    AssignNodeId     = 5  // Used by server to send client its assigned node_id
}


// Protocol
class Packet
{
    public byte     node_id;      // Machine that send the Packet
    public Opcode   opcode;       // Intruction
    public byte     id_buf;       // Buffer ID to operate
    public int      len;          // Length of content
    public string   content = ""; // Content to copy/paste


    public Packet(byte node_id, Opcode opcode, byte id_buf, string content)
    {
        this.node_id  = node_id;
        this.opcode   = opcode;
        this.id_buf   = id_buf;
        this.len      = Encoding.UTF8.GetByteCount(content);;
        this.content  = content;
    }

    // Constructor for FullSync packets
    public Packet(byte node_id, Opcode opcode)
    {
        this.node_id = node_id;
        this.opcode  = opcode;
        this.id_buf  = 0;
        this.len     = 0;
        this.content = "";
    }


    public byte[] ToBytes()
    {
        byte[] packetBytes  = new byte[sizeof(byte)+sizeof(Opcode)+sizeof(byte)+sizeof(int)+this.len]; // Create new byte array of size received packet

        byte[] lenBytes     = BitConverter.GetBytes(this.len);        // Get bytes for packet.len;
        byte[] contentBytes = Encoding.UTF8.GetBytes(this.content);   // Get bytes for packet.content;

        // Fill up byte array
        int idx = 0;

        packetBytes[idx] = this.node_id;  // node_id
        idx++;

        packetBytes[idx] = (byte)this.opcode; // opcode
        idx++;

        packetBytes[idx] = this.id_buf;   // id_buf
        idx++;

        Array.Copy(lenBytes, 0, packetBytes, idx, lenBytes.Length); // len
        idx += lenBytes.Length;

        Array.Copy(contentBytes, 0, packetBytes, idx, contentBytes.Length); // content

        return packetBytes;
    }


    static public Packet FromBytes(byte[] packetBytes)
    {
        int idx = 0;

        byte node_id = packetBytes[idx];
        idx++;

        Opcode opcode = (Opcode)packetBytes[idx];
        idx++;

        byte id_buf = packetBytes[idx];
        idx++;

        int len = BitConverter.ToInt32(packetBytes, idx);
        idx += sizeof(int);

        string content = Encoding.UTF8.GetString(packetBytes, idx, len);

        return new Packet(node_id, opcode, id_buf, content);
    }

}
