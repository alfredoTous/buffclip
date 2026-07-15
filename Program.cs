using static X11;

using System.Diagnostics;

class Program
{

    static void CopyToBuffer(string[] buffers, int id_buf)
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

        buffers[id_buf] = content;
    }


    static void PasteFromBuffer(string[] buffers, int id_buf)
    {
    }

    static void Main()
    {
        
        int NUMBER_OF_BUFFERS = 2;
        string[] buffers = new string[NUMBER_OF_BUFFERS];

        // Manage key press with X11 APIs for global key grab
        IntPtr dpy = XOpenDisplay(IntPtr.Zero); // Open connection to the X11 server
        if (dpy == IntPtr.Zero) {
            Console.WriteLine("[-] Error XOpenDisplay failed");
            return;
        }

        IntPtr rootWindow = XDefaultRootWindow(dpy); // Get root window

        int keycode = XKeysymToKeycode(dpy, XK_F1); // Translate from logic virtual key value to physical keycode

        int result = XGrabKey(dpy, keycode, 0, rootWindow, true, GrabModeAsync, GrabModeAsync);
        Console.WriteLine($"Grab result = {result}");
        XSync(dpy, false);
        
        Console.WriteLine("[i] Esperando F1...");
        XEvent ev;
        
        try {
            while (true) {
                XNextEvent(dpy, out ev);

                if (ev.type == KeyPress) {
                    Console.WriteLine("[+] F1 Presionado");
                    CopyToBuffer(buffers, 1);
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
        }

    }
}


