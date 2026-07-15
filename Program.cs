using static X11;

using System.Diagnostics;


class Buffers
{
    public int NumberOfBuffers;
    public string[] buffers;

    public Buffers(int NumberOfBuffers) {
        this.NumberOfBuffers = NumberOfBuffers;
        buffers = new string[NumberOfBuffers];
    }
    
    void CopyToBuffer(string[] buffers, int id_buf)
    {
        ProcessStartInfo psi = new ProcessStartInfo("xclip", "-sel PRIMARY -o") {
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi);
        if (p == null) {
            Console.WriteLine($"[-] Error xclip failed to initialize");
            return;
        } 
        string content = p.StandardOutput.ReadToEnd();
        Console.WriteLine($"{content}");
        p.WaitForExit();

        this.buffers[id_buf] = content;
    }

    public void PasteFromBuffer(IntPtr dpy) {
        uint h = (uint)XKeysymToKeycode(dpy, XK_H);
        uint i = (uint)XKeysymToKeycode(dpy, XK_I);

        // H
        XTestFakeKeyEvent(dpy, h, true, 0); // Press
        XTestFakeKeyEvent(dpy, h, false, 0); // Release

        // I
        XTestFakeKeyEvent(dpy, i, true, 0); // Press
        XTestFakeKeyEvent(dpy, i, false, 0); // Release

        XFlush(dpy);
    }



}


class Program
{

    
    
    static void Main()
    {

        int NUMBER_OF_BUFFERS = 2;
        Buffers buffers = new Buffers(NUMBER_OF_BUFFERS);

        // Manage key press with X11 APIs for global key grab
        IntPtr dpy = XOpenDisplay(IntPtr.Zero); // Open connection to the X11 server
        if (dpy == IntPtr.Zero) {
            Console.WriteLine("[-] Error XOpenDisplay failed");
            return;
        }

        IntPtr rootWindow = XDefaultRootWindow(dpy); // Get root window

        int f1_keycode = XKeysymToKeycode(dpy, XK_F1); // Translate from logic virtual key value to physical keycode

        XGrabKey(dpy, f1_keycode, 0, rootWindow, true, GrabModeAsync, GrabModeAsync);
        XSync(dpy, false);
        
        Console.WriteLine("[i] Esperando F1...");
        XEvent ev;
        try {
            while (true) {
                XNextEvent(dpy, out ev);

                if (ev.type == KeyRelease && ev.xkey.keycode == f1_keycode) {
                    buffers.PasteFromBuffer(dpy);
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
        }

    }
}


