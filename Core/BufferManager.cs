
class BuffersManager
{
    public int NumberOfBuffers;
    public string[] buffers;
    private ClipboardManager clip;

    // Constructor
    public BuffersManager(int NumberOfBuffers)
    {
        this.NumberOfBuffers = NumberOfBuffers;
        this.buffers = new string[this.NumberOfBuffers];
        for (int i = 0; i < this.NumberOfBuffers; i++)
            buffers[i] = "";

        this.clip = new ClipboardManager();
    }


    // Get content of PRIMARY selection and save it into indicated buffer
    public void CopyToBuffer(int id_buf)
    {
        string content = this.clip.GetClipBoardContent("PRIMARY");
        this.SetBuf(id_buf, content);
        
    }


    // Paste the content of the indicated buffer
    public void PasteFromBuffer(IntPtr dpy, int id_buf)
    {
        // Temporarily save content of CLIPBOARD
        string content = this.clip.GetClipBoardContent("CLIPBOARD");

        // Change content of CLIPBOARD to buffer content
        this.clip.SetClipboardContent("CLIPBOARD", this.GetBuf(id_buf));

        // SimulateCtrlShiftV KeyPress
        HotkeyManager.SimulateCtrlShiftV(dpy);
        Thread.Sleep(20);
        // Return original value
        this.clip.SetClipboardContent("CLIPBOARD", content);
    }


    public bool IsDifferentContent(int id_buf)
    {
        return this.clip.GetClipBoardContent("PRIMARY") != this.GetBuf(id_buf);
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

