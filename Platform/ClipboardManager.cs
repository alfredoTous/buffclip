using static X11;
using System.Text;

public class ClipboardManager 
{
    IntPtr display;
    IntPtr window;
    IntPtr atomPrimary;
    IntPtr atomClipboard;
    IntPtr atomTargets;
    IntPtr atomUTF8;
    private byte[] currentContentBytes = Array.Empty<byte>();

    
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
        this.currentContentBytes = Encoding.UTF8.GetBytes(content);
        XSetSelectionOwner(this.display, selection == "CLIPBOARD" ? this.atomClipboard : this.atomPrimary, this.window, CurrentTime);
    }


    public void HandleSelectionRequest(XSelectionRequestEvent request)
    {
        if (XGetSelectionOwner(this.display, this.atomClipboard) == this.window && request.selection == this.atomClipboard) // Verify we own the selection
        {
            if (request.target == this.atomTargets && request.property != None) // The client is asking for the formats we support
            {
                // We chance the property 'target' on the client window, now is value is the format we support (UTF8)
                 XChangeProperty(request.display, request.requestor, request.property, XA_ATOM, 32, PropModeReplace, ref this.atomUTF8, 1);             }

            else if (request.target == this.atomUTF8 && request.property != None) // The client is asking for the actual value of the selection
            {
                // We change the property 'UTF8' on the client window, no is value is the bytes of the content of the selection we own
                XChangeProperty(request.display, request.requestor, request.property, request.target, 8, PropModeReplace, this.currentContentBytes, this.currentContentBytes.Length);
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

            XSendEvent(this.display, request.requestor, 0, 0, ref xEvent); // Notify the client about the changes on his properties
        }

    }


    // Uses X11 Apis to Get content of PRIMARY/CLIPBOARD selection
    public string GetClipBoardContent(string selection)
    {
        return "hola";
    }

    // Calls XCLIP and SETS the content for a selection
    public void SetClipboardContent(string selection, string content)
    {
        
    }

    ~ClipboardManager()
    {
        XCloseDisplay(this.display);
    }
}

