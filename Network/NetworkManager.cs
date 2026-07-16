using System.Text;
using System.Net;
using System.Net.Sockets;

class NetworkManager
{
    private int port;

    public NetworkManager(int port)
    {
        this.port = port;
    }

    public void StartServer()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, this.port);
        listener.Start();
        Console.WriteLine($"[+] Server listening on {IPAddress.Any}:{this.port}...");

        // Accept client connection
        using TcpClient client = listener.AcceptTcpClient();
        Console.WriteLine("Client connected.");

        NetworkStream NetStream = client.GetStream();
        byte[] tcpBuffer = new byte[1024];
        int bytesRead = NetStream.Read(tcpBuffer, 0, tcpBuffer.Length);

        string message = Encoding.UTF8.GetString(tcpBuffer, 0, tcpBuffer.Length);

        Console.WriteLine($"Received msg: {message}");

        string response = "Hola aaaaa";
        byte[] responseData = Encoding.UTF8.GetBytes(response);
        NetStream.Write(responseData);

    }


}

enum Opcode : byte
{
    UpdateBuffer  = 1, // Update buffer
    FullSync      = 2  // When a new machine is connected
}


// Protocol
class Packet
{
    public byte     node_id;      // Machine that send the Packet (maybe change for IP)
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


    static public byte[] ToBytes(Packet packet)
    {
        byte[] packetBytes  = new byte[sizeof(byte)+sizeof(Opcode)+sizeof(byte)+sizeof(int)+packet.len]; // Create new byte array of size received packet

        byte[] lenBytes     = BitConverter.GetBytes(packet.len);        // Get bytes for packet.len;
        byte[] contentBytes = Encoding.UTF8.GetBytes(packet.content);   // Get bytes for packet.content;

        // Fill up byte array
        int idx = 0;

        packetBytes[idx] = packet.node_id;  // node_id
        idx++;

        packetBytes[idx] = (byte)packet.opcode; // opcode
        idx++;

        packetBytes[idx] = packet.id_buf;   // id_buf
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
