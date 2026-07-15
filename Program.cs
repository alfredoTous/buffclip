using static X11;

class Program
{
    static void Main()
    {
        // Manage key press with X11 APIs for full key grab

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
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
        }

    }
}


