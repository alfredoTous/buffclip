
class BuffersManager
{
    public int NumberOfBuffers;
    public string[] buffers;

    // Constructor
    public BuffersManager(int NumberOfBuffers)
    {
        this.NumberOfBuffers = NumberOfBuffers;
        this.buffers = new string[this.NumberOfBuffers];
        for (int i = 0; i < this.NumberOfBuffers; i++)
            buffers[i] = "";

        //this.clip = new ClipboardManager();
    }

    public bool IsDifferentContent(string content, int id_buf)
    {
        return this.GetBuf(id_buf) != content;
    }

    public string GetBuf(int id_buf)
    {
        if (id_buf < 1 || id_buf > buffers.Length)
            throw new ArgumentOutOfRangeException(nameof(id_buf));

        return this.buffers[id_buf-1];
    }

    public void SetBuf(int id_buf, string content)
    {
        if (id_buf < 1 || id_buf > buffers.Length)
        throw new ArgumentOutOfRangeException(nameof(id_buf));

        this.buffers[id_buf-1] = content;
    }

}

