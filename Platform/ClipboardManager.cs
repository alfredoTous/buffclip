using static X11;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

public class ClipboardManager 
{
    public event Action<byte, string>? PrimaryContentReceived;  // Event to trigger buffer update and network forwards
    IntPtr display;
    IntPtr window;
    IntPtr atomPrimary;
    IntPtr atomClipboard;
    IntPtr atomTargets;
    IntPtr atomUTF8;
    IntPtr negotiatedTarget = IntPtr.Zero;

    private byte[] pendingContentBytes = Array.Empty<byte>(); // Used to know buffer content

    private byte pendingBufferId;                             // Used to know buffer id

    private bool pendingPaste = false;
    private byte pendingPasteBufferId;



    public ClipboardManager(IntPtr display)
    {
        this.display = display; // Receive XServer connection from HotkeyManager
        this.window = XCreateSimpleWindow(display, XDefaultRootWindow(display), 0, 0, 100, 100, 0, 0, 0); // Create invisible window

        this.atomPrimary   = XInternAtom(this.display, "PRIMARY", 0);
        this.atomClipboard = XInternAtom(this.display, "CLIPBOARD", 0);
        this.atomTargets   = XInternAtom(this.display, "TARGETS", 0);
        this.atomUTF8      = XInternAtom(this.display, "UTF8_STRING", 0);
    }


    public void BecomeOwner(string content, string selection)
    {
        this.pendingContentBytes = Encoding.UTF8.GetBytes(content);
        XSetSelectionOwner(display, selection == "CLIPBOARD" ? this.atomClipboard : this.atomPrimary, this.window, CurrentTime);
    }

    // SelectionRequests events happens when a client tries to paste the content of a selection we are owners
    public void HandleSelectionRequest(XSelectionRequestEvent request)
    {
        if (XGetSelectionOwner(display, this.atomClipboard) == this.window && request.selection == this.atomClipboard) // Verify we own the selection
        {
            if (request.target == this.atomTargets && request.property != None) // The client is asking for the formats we support
            {
                // We chance the property 'target' on the client window, now its value is the format we support (UTF8)
                 XChangeProperty(request.display, request.requestor, request.property, XA_ATOM, 32, PropModeReplace, ref this.atomUTF8, 1);             }

            else if (request.target == this.atomUTF8 && request.property != None) // The client is asking for the actual value of the selection
            {
                // We change the property 'UTF8' on the client window, no its value is the bytes of the content of the selection we own
                XChangeProperty(request.display, request.requestor, request.property, request.target, 8, PropModeReplace, this.pendingContentBytes, this.pendingContentBytes.Length);
            }

            XSelectionEvent sendEvent = new XSelectionEvent {
               type = SelectionNotify,
               serial = request.serial,
               send_event = request.send_event,
               display = request.display,
               requestor = request.requestor,
               selection = request.selection,
               target = request.target,
               property = request.property,
               time = request.time
            };

            XEvent xEvent = new XEvent
            {
                xselection = sendEvent
            };

            XSendEvent(display, request.requestor, 0, 0, ref xEvent); // Notify the client about the changes on properties
        }

    }


    public void RequestSelectionContent(string selection, byte? id_buf = null) // Triggers SelectionNotify event
    {
        if (id_buf != null)
            this.pendingBufferId = (byte)id_buf;

        IntPtr atomSelection = selection == "CLIPBOARD" ? this.atomClipboard : this.atomPrimary;
        XConvertSelection(display, atomSelection, this.atomTargets, atomSelection, this.window, CurrentTime);
    }

    public void HandleSelectionNotify(XSelectionEvent selection)
    {
        if (selection.property != None)
        {
            IntPtr actualType;
            int actualFormat;
            IntPtr count;
            IntPtr bytesAfter;
            IntPtr data;

            XGetWindowProperty(
                display,
                window,
                selection.property,
                IntPtr.Zero,
                (IntPtr)int.MaxValue,
                false,
                AnyPropertyType,
                out actualType,
                out actualFormat,
                out count,
                out bytesAfter,
                out data
            );
            
            if (selection.target == this.atomTargets) // Ask for targets
            {
                IntPtr[] list = new IntPtr[count.ToInt32()];
                Marshal.Copy(data, list, 0, list.Length);

                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i] == XA_STRING)
                    {
                        this.negotiatedTarget = XA_STRING;
                    }
                    else if (list[i] == atomUTF8)
                    {
                        this.negotiatedTarget = atomUTF8;
                        break;
                    }
                }

                if (this.negotiatedTarget != IntPtr.Zero)
                {
                    XConvertSelection(this.display, selection.selection, this.negotiatedTarget, selection.selection, this.window, CurrentTime);
                }
            } 
            else if (selection.target == this.negotiatedTarget) // Got data
            {
                string? decodedData = Marshal.PtrToStringUTF8(data, count.ToInt32());
                if (selection.selection == this.atomPrimary)
                    this.PrimaryContentReceived?.Invoke(pendingBufferId, decodedData!); // Trigger event that changes buffer content and forwards network packets
                if (selection.selection == this.atomClipboard && this.pendingPaste == true)
                {
                    this.FinishPasteBufferContent(decodedData!);
                }
            }
            if (data != IntPtr.Zero) XFree(data);
        }
    }


    public void BeginPasteBufferContent(byte id_buf)
    {
        this.pendingPasteBufferId = id_buf;
        this.pendingPaste = true;
        RequestSelectionContent("CLIPBOARD"); // Request clipboard content for backup (Triggers SelectionNotify)
    }

    private void FinishPasteBufferContent(string clipboardContentBackup)
    {
        this.pendingPaste = false;
        
        // Own clipboard selection to temporarily change its content
        BecomeOwner(Globals.BuffersManager.GetBuf(this.pendingPasteBufferId), "CLIPBOARD");
        HotkeyManager.SimulateCtrlShiftV(display); // Simulate Ctrl+shift+v

        this.WaitAndRestoreClipboard(clipboardContentBackup);

    }

    // Clients keeps sending events so we can't just restore clipboard immediately, so we use a lil wait
    private void WaitAndRestoreClipboard(string backupContent)
    {
        const int quietMs = 30;    // No events activity for this time, we assume client stopped sending events and can now restore clipboard
        const int hardCapMs = 400; // Max waiting

        var overall = Stopwatch.StartNew();
        var sinceLastServe = Stopwatch.StartNew();

        while (sinceLastServe.ElapsedMilliseconds < quietMs && overall.ElapsedMilliseconds < hardCapMs)
        {
            if (XPending(display) > 0)
            {
                XEvent ev;
                XNextEvent(display, out ev);

                if (ev.type == SelectionRequest)
                {
                    HandleSelectionRequest(ev.xselectionrequest);
                    if (ev.xselectionrequest.target == atomUTF8)
                        sinceLastServe.Restart(); // We receive more events from client, restart timer
                }
                else
                {
                    HotkeyManager.HandleXEvent(ev); // Don't lose other events that may arrive
                }
            }
            else
            {
                Thread.Sleep(2);
            }
        }

        BecomeOwner(backupContent, "CLIPBOARD");
    }

}

