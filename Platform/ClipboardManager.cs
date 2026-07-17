using static X11;
using System.Runtime.InteropServices;
using System.Diagnostics;


public class ClipboardManager 
{
    IntPtr display;
    IntPtr atomPrimary;
    IntPtr atomClipboard;
    IntPtr atomUTF8;
    IntPtr atomString;
    IntPtr atomProperty;

    
    public ClipboardManager()
    {
        this.display = XOpenDisplay(IntPtr.Zero); // Opens a new connection to the X server
        if (this.display == IntPtr.Zero)
            throw new Exception("[-] Error XOpenDisplay failed");
        
        this.atomPrimary = XInternAtom(display, "PRIMARY", 0);
        this.atomClipboard = XInternAtom(display, "CLIPBOARD", 0);
        this.atomUTF8 = XInternAtom(display, "UTF8_STRING", 0);
        this.atomString = XInternAtom(display, "STRING", 0);
        this.atomProperty = XInternAtom(display, "BUFFCLIP_PROPERTY", 0);

    }

    // Uses X11 Apis to Get content of PRIMARY/CLIPBOARD selection
    public string GetClipBoardContent(string selection)
    {
        int screen = XDefaultScreen(this.display);
        IntPtr window = XCreateSimpleWindow(display, XRootWindow(display, screen), 0, 0, 1, 1, 0, 0, 0); // Create invisible window to request clipboard content
        
        try {
            // Ask for content in UTF8 format
            XConvertSelection(display, selection == "PRIMARY" ? this.atomPrimary : this.atomClipboard, this.atomUTF8, this.atomProperty, window, CurrentTime);
            XFlush(display);

            XEvent xEvent;
            while (true) {
                XNextEvent(display, out xEvent);
                if (xEvent.type == SelectionNotify) { // Received answer from owner
                    ref XSelectionEvent sel = ref xEvent.xselection;

                    if (sel.property == None)
                        return "";

                    IntPtr actualType = IntPtr.Zero;
                    int actualFormat = 0;
                    IntPtr itemsCount = IntPtr.Zero;
                    IntPtr bytesAfter = IntPtr.Zero;
                    IntPtr data = IntPtr.Zero;

                    XGetWindowProperty(this.display, window, sel.property, IntPtr.Zero, new IntPtr(-1), 0, AnyPropertyType, ref actualType, ref actualFormat, ref itemsCount, ref bytesAfter, ref data);

                    string result = "";
                    if (data != IntPtr.Zero) {
                        result = Marshal.PtrToStringAnsi(data)!;
                        XFree(data);
                    }
                    return result;
                }
            }
        } finally {
            XDestroyWindow(this.display, window);
            XFlush(this.display);
        }
    }

    // Calls XCLIP and SETS the content for a selection
    public void SetClipboardContent(string selection, string content) {
        ProcessStartInfo psi = new ProcessStartInfo("xclip", $"-sel {selection}") {
            RedirectStandardInput = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi);
        if (p == null)
            throw new Exception("[-] Error xclip failed to initialize");
        p.StandardInput.Write(content);
        p.StandardInput.Close();
        p.WaitForExit();
    }

    ~ClipboardManager()
    {
        XCloseDisplay(this.display);
    }
}

