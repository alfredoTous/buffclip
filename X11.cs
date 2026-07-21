using System.Runtime.InteropServices;

// X11 Definitions
public class X11
{
    // ========== CONSTANTS ==========
    
    // Time
    public const uint CurrentTime = 0;
    
    // Event Masks & Modifiers
    public const uint AnyModifier = (1u << 15);
    public const uint LockMask = 1u << 1;      // Caps Lock
    public const uint Mod2Mask = 1u << 4;      // Num Lock
    
    // Properties
    public const int AnyPropertyType = 0;
    public const int PropModeReplace = 0;
    public const uint None = 0;
    public const int NoEventMask = 0;
    
    // Grab modes
    public const int GrabModeAsync = 1;
    
    // ========== EVENT TYPES ==========
    
    public const int KeyPress = 2;
    public const int KeyRelease = 3;
    public const int SelectionClear = 29;
    public const int SelectionRequest = 30;
    public const int SelectionNotify = 31;
    
    // ========== KEYSYMS ==========
    
    public const ulong XK_F1 = 0xffbe;
    public const ulong XK_F2 = 0xFFBF;
    public const ulong XK_F3 = 0xFFC0;
    public const ulong XK_F4 = 0xFFC1;

    public const ulong XK_Control_L = 0xffe3;
    public const ulong XK_Shift_L = 0xffe1;
    public const ulong XK_V = 0x0076;
    
    // ========== STRUCTS ==========
    
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

    [StructLayout(LayoutKind.Sequential)]
    public struct XSelectionRequestEvent
    {
        public int type;
        public IntPtr serial;
        public int send_event;
        public IntPtr display;
        public IntPtr owner;
        public IntPtr requestor;
        public IntPtr selection;
        public IntPtr target;
        public IntPtr property;
        public uint time;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XSelectionEvent
    {
        public int type;
        public IntPtr serial;
        public int send_event;
        public IntPtr display;
        public IntPtr requestor;
        public IntPtr selection;
        public IntPtr target;
        public IntPtr property;
        public uint time;
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct XEvent
    {
        [FieldOffset(0)]
        public int type;
        
        [FieldOffset(0)]
        public XKeyEvent xkey;
        
        [FieldOffset(0)]
        public XSelectionRequestEvent xselectionrequest;
        
        [FieldOffset(0)]
        public XSelectionEvent xselection;
    }
    
    // ========== PINVOKES ==========
    
    [DllImport("libX11.so.6")]
    public static extern IntPtr XOpenDisplay(IntPtr display);
    
    [DllImport("libX11.so.6")]
    public static extern int XCloseDisplay(IntPtr display);
    
    [DllImport("libX11.so.6")]
    public static extern IntPtr XDefaultRootWindow(IntPtr display);
    
    [DllImport("libX11.so.6")]
    public static extern int XDefaultScreen(IntPtr display);
    
    [DllImport("libX11.so.6")]
    public static extern IntPtr XRootWindow(IntPtr display, int screen_number);
    
    [DllImport("libX11.so.6")]
    public static extern IntPtr XInternAtom(IntPtr display, string atom_name, int only_if_exists);
    
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
    public static extern int XPending(IntPtr display);
    
    [DllImport("libX11.so.6")]
    static extern int XSelectInput(IntPtr display, IntPtr window, long mask);
    
    [DllImport("libX11.so.6")]
    public static extern int XSync(IntPtr display, bool discard);
    
    [DllImport("libX11.so.6")]
    public static extern int XFlush(IntPtr display);
    
    [DllImport("libX11.so.6")]
    public static extern int XSetSelectionOwner(IntPtr display, IntPtr selection, IntPtr owner, uint time);
    
    [DllImport("libX11.so.6")]
    public static extern IntPtr XGetSelectionOwner(IntPtr display, IntPtr selection);
    
    [DllImport("libX11.so.6")]
    public static extern int XConvertSelection(IntPtr display, IntPtr selection, IntPtr target,
        IntPtr property, IntPtr requestor, uint time);
    
    [DllImport("libX11.so.6")]
    public static extern int XSendEvent(IntPtr display, IntPtr w, int propagate, IntPtr event_mask, ref XEvent event_send);
    
    [DllImport("libX11.so.6")]
    public static extern int XChangeProperty(IntPtr display, IntPtr w, IntPtr property, IntPtr type,
        int format, int mode, byte[] data, int nelements);
    
    [DllImport("libX11.so.6")]
    public static extern int XChangeProperty(IntPtr display, IntPtr w, IntPtr property, IntPtr type,
        int format, int mode, IntPtr data, int nelements);

public static IntPtr XA_ATOM   = (IntPtr)4;
public static IntPtr XA_STRING = (IntPtr)31;

    [DllImport("libX11.so.6")]
public static extern int XChangeProperty(
    IntPtr display,
    IntPtr w,
    IntPtr property,
    IntPtr type,
    int format,
    int mode,
    ref IntPtr data,
    int nelements
);
    
    [DllImport("libX11.so.6")]
    public static extern int XGetWindowProperty(IntPtr display, IntPtr w, IntPtr property, IntPtr long_offset,
        IntPtr long_length, int delete, IntPtr req_type, ref IntPtr actual_type_return, ref int actual_format_return,
        ref IntPtr nitems_return, ref IntPtr bytes_after_return, ref IntPtr prop_return);

    [DllImport("libX11.so.6")]
public static extern int XGetWindowProperty(
    IntPtr display,
    IntPtr w,
    IntPtr property,
    IntPtr long_offset,
    IntPtr long_length,
    bool delete,
    IntPtr req_type,
    out IntPtr actual_type,
    out int actual_format,
    out IntPtr nitems,
    out IntPtr bytes_after,
    out IntPtr prop
);
    
    [DllImport("libX11.so.6")]
    public static extern int XDeleteProperty(IntPtr display, IntPtr w, IntPtr property);
    
    [DllImport("libX11.so.6")]
    public static extern IntPtr XCreateSimpleWindow(IntPtr display, IntPtr parent, int x, int y,
        uint width, uint height, uint border_width, uint border, uint background);
    
    [DllImport("libX11.so.6")]
    public static extern int XDestroyWindow(IntPtr display, IntPtr w);
    
    [DllImport("libX11.so.6")]
    public static extern int XFree(IntPtr data);
    
    [DllImport("libXtst.so.6")]
    public static extern int XTestFakeKeyEvent(
        IntPtr display,
        uint keycode,
        bool is_press,
        ulong delay);
    
}
