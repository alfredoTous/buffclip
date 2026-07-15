using System.Runtime.InteropServices;

class X11 
{
    public const int KeyPress = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct XKeyEvent
    {
        public int type;
        public IntPtr serial;
        public int send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public IntPtr time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public uint keycode;
        public int same_screen;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct XEvent
    {
        [FieldOffset(0)]
        public int type;

        [FieldOffset(0)]
        public XKeyEvent xkey;
    }

    [DllImport("libX11.so.6")]
    public static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    public static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    public static extern int XKeysymToKeycode(IntPtr display, ulong keysym);

    [DllImport("libX11.so.6")]
    public static extern int XGrabKey(
        IntPtr display,
        int keycode,
        uint modifiers,
        IntPtr grab_window,
        bool owner_events,
        int pointer_mode,
        int keyboard_mode);

    [DllImport("libX11.so.6")]
    public static extern int XNextEvent(IntPtr display, out XEvent xevent);

    [DllImport("libX11.so.6")]
    static extern int XSelectInput(IntPtr display, IntPtr window, long mask);

    [DllImport("libX11.so.6")]
    public static extern int XSync(IntPtr display, bool discard);

    public const int GrabModeAsync = 1;

    public const ulong XK_F1 = 0xffbe;

}
