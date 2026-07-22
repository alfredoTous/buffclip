using static X11;
using System.Diagnostics;


class HotkeyManager
{

    static IntPtr display = IntPtr.Zero;
    static ClipboardManager clipboard = null!;

    static int f1_keycode;
    static int f2_keycode;
    static int f3_keycode;
    static int f4_keycode;

    private static Stopwatch autoRepeatStopwatchF2 = new Stopwatch();
    private static bool isAutoRepeatingF2 = false;

    private static Stopwatch autoRepeatStopwatchF4 = new Stopwatch();
    private static bool isAutoRepeatingF4 = false;

    public static int ignoreF2PressCount = 0;
    public static int ignoreF2ReleaseCount = 0;
    public static int ignoreF4PressCount = 0;
    public static int ignoreF4ReleaseCount = 0;

    private static class KeyState
    {
        public static bool f1Down = false;
        public static bool f2Down = false;
        public static bool f3Down = false;
        public static bool f4Down = false;
       
        // When F1+F2+F3+F4 are pressed we activate/deactivate the key global grabbing
        public static bool combo => (KeyState.f1Down && KeyState.f2Down && KeyState.f3Down && KeyState.f4Down);
        public static bool toggleComboActive = false;

        public static bool CheckCombo()
        {
            if (KeyState.combo)
            {
                if (!KeyState.toggleComboActive)
                {
                    KeyState.toggleComboActive = true;
                    return true;
                }
            }
            else
            {
                KeyState.toggleComboActive = false;
            }
            return false;
        }
    }

    // Main Thread
    public static void ListenForKeyPress(NetworkManager network)
    {
        // Manage key press and selection events with X11 APIs

        display = XOpenDisplay(IntPtr.Zero); // Open connection to the X11 server
        if (display == IntPtr.Zero)
            throw new Exception("[-] Error XOpenDisplay failed");


        // ===================== CLIPBOARD =================================================================
        clipboard = new ClipboardManager(display);
        // Subscribe to event
        // This event is trigerred on F1/F3 keypresses by clipboard.HandleSelectionNotify
        clipboard.PrimaryContentReceived += (bufferId, content) =>
        {
            if (!Globals.BuffersManager.IsDifferentContent(content, bufferId)) return; // Avoid actions if content is the same

            Globals.BuffersManager.SetBuf(bufferId, content);
            if (network.IsConnected)
                network.SendUpdateBuffer(bufferId);
        };
        // =================================================================================================

        IntPtr rootWindow = XDefaultRootWindow(display); // Get root window, needed for Global Grab of F1/F2/F3/F4 keys

        f1_keycode = XKeysymToKeycode(display, XK_F1); // Translate from logic virtual key (F1) value to physical keycode
        f2_keycode = XKeysymToKeycode(display, XK_F2); // Translate from logic virtual key (F2) value to physical keycode
        f3_keycode = XKeysymToKeycode(display, XK_F3); // Translate from logic virtual key (F3) value to physical keycode
        f4_keycode = XKeysymToKeycode(display, XK_F4); // Translate from logic virtual key (F4) value to physical keycode


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
            // Main loop
            while (true) {
                if (XPending(display) > 0)
                {
                    XNextEvent(display, out ev);
                    HandleXEvent(ev);
                }
                else
                {
                    if (isAutoRepeatingF2)
                    {
                        bool f2Down = IsPhysicalKeyDown(display, f2_keycode);
                        // We use a 150ms buffer to allow physical key state to settle or fake release to not break us immediately
                        if (!f2Down && autoRepeatStopwatchF2.ElapsedMilliseconds > 150)
                        {
                            isAutoRepeatingF2 = false;
                            Console.WriteLine("Auto-repeat F2 STOPPED.");
                            autoRepeatStopwatchF2.Stop();
                        }
                        else if (f2Down && autoRepeatStopwatchF2.ElapsedMilliseconds > 150) // 150ms auto-repeat delay (faster)
                        {
                            if (!clipboard.IsPasting) 
                            {
                                Console.WriteLine("Auto-repeat F2 TRIGGER!");
                                clipboard.BeginPasteBufferContent(1);
                                autoRepeatStopwatchF2.Restart();
                            }
                        }
                    }

                    if (isAutoRepeatingF4)
                    {
                        bool f4Down = IsPhysicalKeyDown(display, f4_keycode);
                        if (!f4Down && autoRepeatStopwatchF4.ElapsedMilliseconds > 150)
                        {
                            isAutoRepeatingF4 = false;
                            Console.WriteLine("Auto-repeat F4 STOPPED.");
                            autoRepeatStopwatchF4.Stop();
                        }
                        else if (f4Down && autoRepeatStopwatchF4.ElapsedMilliseconds > 150)
                        {
                            if (!clipboard.IsPasting) 
                            {
                                Console.WriteLine("Auto-repeat F4 TRIGGER!");
                                clipboard.BeginPasteBufferContent(2);
                                autoRepeatStopwatchF4.Restart();
                            }
                        }
                    }

                    System.Threading.Thread.Sleep(10);
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
        }
    }


    public static void HandleXEvent(XEvent ev)
    {
        switch (ev.type)
        {
            case KeyPress:
            { 
                UpdateKeyState(ev.xkey, true);

                if (KeyState.CheckCombo())
                {
                    Console.WriteLine("TOGGLE!");
                    return;
                }
                // Buffer 1
                if (ev.xkey.keycode == f1_keycode) 
                { 
                    // Triggers SelectionNotify event (XConvertSelection Api)
                    Console.WriteLine("F1 ACTION");
                    clipboard.RequestSelectionContent("PRIMARY", 1);
                    // This usually triggers 2 SelectionNotify events, first to get the targets (formats we support) and the other with the actual content of the selection (in this case PRIMARY)
                    // When the actual content of the selection is got, clipboard.HandleSelectionNotify internally invokes PrimaryContentReceived (event we subscribe) 
                    // This event copies content to buffer[id_buf] and forwards to network if connected

                }

                if (ev.xkey.keycode == f2_keycode)
                {
                    if (ignoreF2PressCount > 0)
                    {
                        ignoreF2PressCount--;
                        return; // Ignore fake press
                    }
                    if (clipboard.IsPasting) return;
                    
                    // Prevent overlapping pastes from X11 auto-repeat if it kicks in
                    if (isAutoRepeatingF2 && autoRepeatStopwatchF2.ElapsedMilliseconds < 120)
                        return;

                    // Begin pasting of buffer content
                    Console.WriteLine("F2 ACTION");
                    clipboard.BeginPasteBufferContent(1);
                    isAutoRepeatingF2 = true;
                    autoRepeatStopwatchF2.Restart();
                    // It requests CLIPBOARD content for backup which triggers a SelectionNotify event
                    // Then it simulates CTRL+SHIFT+V for pasting
                    // After a timeout it restores CLIPBOARD content

                }

                // Buffer 2
                if (ev.xkey.keycode == f3_keycode) 
                {
                    Console.WriteLine("F3 ACTION");
                    clipboard.RequestSelectionContent("PRIMARY", 2);
                }

                if (ev.xkey.keycode == f4_keycode)
                {
                    if (ignoreF4PressCount > 0)
                    {
                        ignoreF4PressCount--;
                        return; 
                    }
                    if (clipboard.IsPasting) return;
                    
                    if (isAutoRepeatingF4 && autoRepeatStopwatchF4.ElapsedMilliseconds < 120)
                        return;

                    Console.WriteLine("F4 ACTION");
                    clipboard.BeginPasteBufferContent(2);
                    isAutoRepeatingF4 = true;
                    autoRepeatStopwatchF4.Restart();
                }

                break;
            }

            case KeyRelease:
            {
                if (ev.xkey.keycode == f2_keycode && ignoreF2ReleaseCount > 0)
                {
                    ignoreF2ReleaseCount--;
                    return; // Ignore fake release
                }
                if (ev.xkey.keycode == f4_keycode && ignoreF4ReleaseCount > 0)
                {
                    ignoreF4ReleaseCount--;
                    return; 
                }
                UpdateKeyState(ev.xkey, false);
                break;
            }

            case SelectionRequest:
            {
                XSelectionRequestEvent request = ev.xselectionrequest; // Get request
                clipboard.HandleSelectionRequest(request);
                break;
            }

            case SelectionNotify:
            {
                XSelectionEvent selection = ev.xselection;
                clipboard.HandleSelectionNotify(selection);
                break;
            }
        }
    }


    private static void UpdateKeyState(XKeyEvent key, bool isPressed)
    {
        if (key.keycode == f1_keycode)
            KeyState.f1Down = isPressed;
        if (key.keycode == f2_keycode) 
            KeyState.f2Down = isPressed; 
        if (key.keycode == f3_keycode) 
            KeyState.f3Down = isPressed;
        if (key.keycode == f4_keycode) 
            KeyState.f4Down = isPressed; 
    }


    public static bool IsPhysicalKeyDown(IntPtr display, int keycode)
    {
        byte[] keys = new byte[32];
        X11.XQueryKeymap(display, keys);
        return (keys[keycode / 8] & (1 << (keycode % 8))) != 0;
    }

    // Simulates key strokes for pasting
    public static void SimulateCtrlShiftV(IntPtr display, byte bufferId)
    { 
        uint ctrl  = (uint)XKeysymToKeycode(display, XK_Control_L);
        uint shift = (uint)XKeysymToKeycode(display, XK_Shift_L);
        uint v     = (uint)XKeysymToKeycode(display, XK_V);
        
        int source_keycode = (bufferId == 1) ? f2_keycode : f4_keycode;
        bool wasKeyDown = IsPhysicalKeyDown(display, source_keycode);

        if (wasKeyDown)
        {
            if (source_keycode == f2_keycode) ignoreF2ReleaseCount++;
            XTestFakeKeyEvent(display, (uint)source_keycode, false, 0); // Toca hacer esto para que el paste funcione
        }

        XTestFakeKeyEvent(display, ctrl, true, 0);   // Ctrl Down
        XTestFakeKeyEvent(display, shift, true, 0);  // Shift Down
        XTestFakeKeyEvent(display, v, true, 0);      // V Down

        XTestFakeKeyEvent(display, v, false, 0);     // V Up
        XTestFakeKeyEvent(display, shift, false, 0); // Shift Up
        XTestFakeKeyEvent(display, ctrl, false, 0);  // Ctrl Up

        if (wasKeyDown)
        {
            if (source_keycode == f2_keycode) ignoreF2PressCount++;
            XTestFakeKeyEvent(display, (uint)source_keycode, true, 0); // Restauramos F2 para no perder el estado
        }

        XFlush(display);
    }

}
