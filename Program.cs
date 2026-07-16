using static X11;

using System.Diagnostics;
using System.Runtime.InteropServices;


class ClipboardManager 
{
    IntPtr display;
    IntPtr atomPrimary;
    IntPtr atomClipboard;
    IntPtr atomUTF8;
    IntPtr atomString;
    IntPtr atomProperty;

    
    public ClipboardManager()
    {
        this.display = XOpenDisplay(IntPtr.Zero);
        if (this.display == IntPtr.Zero)
            throw new Exception("[-] Error XOpenDisplay failed");
        
        this.atomPrimary = XInternAtom(display, "PRIMARY", 0);
        this.atomClipboard = XInternAtom(display, "CLIPBOARD", 0);
        this.atomUTF8 = XInternAtom(display, "UTF8_STRING", 0);
        this.atomString = XInternAtom(display, "STRING", 0);
        this.atomProperty = XInternAtom(display, "BUFFCLIP_PROPERTY", 0);

    }

    public string GetClipBoardContent(string selection)
    {
        int screen = XDefaultScreen(this.display);
        IntPtr window = XCreateSimpleWindow(display, XRootWindow(display, screen), 0, 0, 1,1, 0, 0, 0); // Create invisible window to request clipboard content
        
        // Ask for content in UTF8 format
        XConvertSelection(display, selection == "PRIMARY" ? this.atomPrimary : this.atomClipboard, this.atomUTF8, this.atomProperty, window, CurrentTime);
        XFlush(display);

        XEvent xEvent;
        while (true) {
            XNextEvent(display, out xEvent);
            if (xEvent.type == SelectionNotify) { // Received answer from owner
                ref XSelectionEvent sel = ref xEvent.xselection;

                if (sel.property == None)
                    throw new Exception("[-] Owner does not support target format");

                IntPtr actualType = IntPtr.Zero;
                int actualFormat = 0;
                IntPtr itemsCount = IntPtr.Zero;
                IntPtr bytesAfter = IntPtr.Zero;
                IntPtr data = IntPtr.Zero;

                XGetWindowProperty(this.display, window, sel.property, IntPtr.Zero, new IntPtr(-1), 0, AnyPropertyType, ref actualType, ref actualFormat, ref itemsCount, ref bytesAfter, ref data);

                string result = Marshal.PtrToStringAnsi(data)!;
                return result;
            }
        }
    }

    ~ClipboardManager()
    {
        XCloseDisplay(this.display);
    }
}


class Buffers
{
    public int NumberOfBuffers;
    public string[] buffers;
    private ClipboardManager clip;

    // Constructor
    public Buffers(int NumberOfBuffers)
    {
        this.NumberOfBuffers = NumberOfBuffers;
        this.buffers = new string[this.NumberOfBuffers];
        this.clip = new ClipboardManager();
    }


    // Get content of PRIMARY selection and save it into indicated buffer
    public void CopyToBuffer(int id_buf)
    {
        string content = this.clip.GetClipBoardContent("PRIMARY");
        this.buffers[id_buf] = content;
    }
    
    // Paste the content of the indicated buffer
    public void PasteFromBuffer(IntPtr dpy, int id_buf)
    {
        // Temporarily save content of CLIPBOARD
        string content = this.clip.GetClipBoardContent("CLIPBOARD");

        // Change content of CLIPBOARD to buffer content
        this.SetClipboardContent("CLIPBOARD", buffers[id_buf]);

        // SimulateCtrlShiftV KeyPress
        this.SimulateCtrlShiftV(dpy);
        Thread.Sleep(20);
        // Return original value
        this.SetClipboardContent("CLIPBOARD", content);
    }

    // Calls XCLIP and SETS the content for a selection
    private void SetClipboardContent(string selection, string content) {
        ProcessStartInfo psi = new ProcessStartInfo("xclip", $"-sel {selection}") {
            RedirectStandardInput = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi);
        if (p == null) {
            Console.WriteLine("[-] Error xclip failed to initialize");
            return;
        }
        p.StandardInput.Write(content);
        p.StandardInput.Close();
        p.WaitForExit();
    }
    
    // Simulates key strokes for pasting
    private void SimulateCtrlShiftV(IntPtr dpy)
    { 
        uint ctrl  = (uint)XKeysymToKeycode(dpy, XK_Control_L);
        uint shift = (uint)XKeysymToKeycode(dpy, XK_Shift_L);
        uint v     = (uint)XKeysymToKeycode(dpy, XK_V);

        XTestFakeKeyEvent(dpy, ctrl, true, 0);   // Ctrl Down
        XTestFakeKeyEvent(dpy, shift, true, 0);  // Shift Down
        XTestFakeKeyEvent(dpy, v, true, 0);      // V Down

        XTestFakeKeyEvent(dpy, v, false, 0);     // V Up
        XTestFakeKeyEvent(dpy, shift, false, 0); // Shift Up
        XTestFakeKeyEvent(dpy, ctrl, false, 0);  // Ctrl Up

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

        int f1_keycode = XKeysymToKeycode(dpy, XK_F1); // Translate from logic virtual key (F1) value to physical keycode
        int f2_keycode = XKeysymToKeycode(dpy, XK_F2); // Translate from logic virtual key (F2) value to physical keycode

        // Used to grab combinations of key such as {key}+CapsLock, etc
        uint[] lockCombos = {
            0,
            LockMask,
            Mod2Mask,
            LockMask | Mod2Mask,
        };
        
        // Keys to grab globally
        int[] used_keys = {
            f1_keycode,
            f2_keycode
        };
       
        foreach (int key in used_keys) {
            foreach (uint mods in lockCombos) {
                XGrabKey(dpy, key, mods, rootWindow, true, GrabModeAsync, GrabModeAsync);
            }
        }
        XSync(dpy, false);
        
        Console.WriteLine("[i] Esperando F1...");
        XEvent ev;
        try {
            while (true) {
                XNextEvent(dpy, out ev);

                if (ev.type == KeyRelease && ev.xkey.keycode == f1_keycode) {
                    buffers.CopyToBuffer(1);
                }
                if (ev.type == KeyRelease && ev.xkey.keycode == f2_keycode) {
                    buffers.PasteFromBuffer(dpy, 1);
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
        }

    }
}


