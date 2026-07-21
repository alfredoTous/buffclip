using static X11;

using System.Text;

class HotkeyManager
{

    // Main Thread
    public static void ListenForKeyPress(NetworkManager network)
    {
        // Manage key press and selection events with X11 APIs

        IntPtr display = XOpenDisplay(IntPtr.Zero); // Open connection to the X11 server
        if (display == IntPtr.Zero)
            throw new Exception("[-] Error XOpenDisplay failed");

        ClipboardManager clipboard = new ClipboardManager(display);

        IntPtr rootWindow = XDefaultRootWindow(display); // Get root window, needed for Global Grab of F1/F2/F3/F4 keys

        int f1_keycode = XKeysymToKeycode(display, XK_F1); // Translate from logic virtual key (F1) value to physical keycode
        int f2_keycode = XKeysymToKeycode(display, XK_F2); // Translate from logic virtual key (F2) value to physical keycode
        int f3_keycode = XKeysymToKeycode(display, XK_F3); // Translate from logic virtual key (F3) value to physical keycode
        int f4_keycode = XKeysymToKeycode(display, XK_F4); // Translate from logic virtual key (F4) value to physical keycode


        // Used to grab combinations of keys such as {key}+CapsLock, etc
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
       
        // Grab
        foreach (int key in used_keys) {
            foreach (uint mods in lockCombos) {
                XGrabKey(display, key, mods, rootWindow, true, GrabModeAsync, GrabModeAsync);
            }
        }
        XSync(display, false);

        Console.WriteLine("[i] Esperando F1...");
        XEvent ev;
        try {
            while (true) {
                XNextEvent(display, out ev);

                switch (ev.type)
                {
                    case KeyRelease:
                    {
                        // Buffer 1
                        if (ev.xkey.keycode == f1_keycode) 
                        {
                            if (!Globals.BuffersManager.IsDifferentContent(1)) continue; // Avoid actions if content is the same

                            Globals.BuffersManager.CopyToBuffer(1);
                            if (network.IsConnected)
                                network.SendUpdateBuffer(1);
                        }

                        if (ev.xkey.keycode == f2_keycode)
                        {
                            clipboard.BecomeOwner(Globals.BuffersManager.GetBuf(1), "CLIPBOARD"); // Set ourselfs as owners of the selection CLIPBOARD
                            SimulateCtrlShiftV(display);        // Simulate SelectionRequest event
                        }

                        // Buffer 2
                        if (ev.xkey.keycode == f3_keycode) 
                        {
                            if (!Globals.BuffersManager.IsDifferentContent(2)) continue; // Avoid actions if content is the same

                            Globals.BuffersManager.CopyToBuffer(2);
                            if (network.IsConnected)
                                network.SendUpdateBuffer(2);
                        }

                        if (ev.xkey.keycode == f4_keycode) 
                            Globals.BuffersManager.PasteFromBuffer(display, 2);

                        break;
                    }

                    case SelectionRequest: // LLegamos aca despues de hacer el SimulateCtrlShiftV
                    {
                        XSelectionRequestEvent request = ev.xselectionrequest; // Get request
                        clipboard.HandleSelectionRequest(request);
                        break;
                    }
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
        }
    }


    // Simulates key strokes for pasting
    public static void SimulateCtrlShiftV(IntPtr display)
    { 
        uint ctrl  = (uint)XKeysymToKeycode(display, XK_Control_L);
        uint shift = (uint)XKeysymToKeycode(display, XK_Shift_L);
        uint v     = (uint)XKeysymToKeycode(display, XK_V);

        XTestFakeKeyEvent(display, ctrl, true, 0);   // Ctrl Down
        XTestFakeKeyEvent(display, shift, true, 0);  // Shift Down
        XTestFakeKeyEvent(display, v, true, 0);      // V Down

        XTestFakeKeyEvent(display, v, false, 0);     // V Up
        XTestFakeKeyEvent(display, shift, false, 0); // Shift Up
        XTestFakeKeyEvent(display, ctrl, false, 0);  // Ctrl Up

        XFlush(display);
    }

}
