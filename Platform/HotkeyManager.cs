using static X11;

class HotkeyManager
{
    // Intentar remover la dependencia de los buffers mas adelante

    public static void ListenForKeyPress()
    {
        // Manage key press with X11 APIs for global key grab
        IntPtr dpy = XOpenDisplay(IntPtr.Zero); // Open connection to the X11 server
        if (dpy == IntPtr.Zero)
            throw new Exception("[-] Error XOpenDisplay failed");

        IntPtr rootWindow = XDefaultRootWindow(dpy); // Get root window

        int f1_keycode = XKeysymToKeycode(dpy, XK_F1); // Translate from logic virtual key (F1) value to physical keycode
        int f2_keycode = XKeysymToKeycode(dpy, XK_F2); // Translate from logic virtual key (F2) value to physical keycode
        int f3_keycode = XKeysymToKeycode(dpy, XK_F3); // Translate from logic virtual key (F3) value to physical keycode
        int f4_keycode = XKeysymToKeycode(dpy, XK_F4); // Translate from logic virtual key (F4) value to physical keycode

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
            f2_keycode,
            f3_keycode,
            f4_keycode
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

                if (ev.type == KeyRelease) {
                    if (ev.xkey.keycode == f1_keycode) 
                        Globals.BuffersManager.CopyToBuffer(0);

                    if (ev.xkey.keycode == f2_keycode) 
                        Globals.BuffersManager.PasteFromBuffer(dpy, 0);

                    if (ev.xkey.keycode == f3_keycode) 
                        Globals.BuffersManager.CopyToBuffer(1);

                    if (ev.xkey.keycode == f4_keycode) 
                        Globals.BuffersManager.PasteFromBuffer(dpy, 1);
            }   }
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
        }
    }


    // Simulates key strokes for pasting
    public static void SimulateCtrlShiftV(IntPtr dpy)
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
