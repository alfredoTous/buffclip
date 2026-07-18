

static class Globals
{
        public static BuffersManager BuffersManager = new BuffersManager(2);  // Initiate 2 Buffers;
}


class Program
{

    static void PrintHelp()
    {
        Console.WriteLine(
    @"Buffclip - Network clipboard manager - Inspired by Kitty terminal

    Usage:
        buffclip server
        buffclip client <server-ip>

    Commands:
        server              Start Buffclip in server mode.
        client <server-ip>  Connect to a Buffclip server.

    Examples:
        buffclip server
        buffclip client 192.168.1.100");
    }


    static BuffclipServer InitServer()
    {
        BuffclipServer server = new BuffclipServer("0.0.0.0", 4443);    // Default values for now
        Thread thread = new Thread(server.Start);                       // Starts server and handles client connections
        thread.IsBackground = true;
        thread.Start();
        return server;
    }

    static BuffclipClient InitClient()
    {
        BuffclipClient client = new BuffclipClient("192.168.1.47", 4443);
        Thread thread = new Thread(client.Start);
        thread.IsBackground = true;
        thread.Start();
        return client;
    }

    
    static void Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland") {
            Console.WriteLine("[-] Wayland session detected");
            Console.WriteLine("[-] Buffclip is currently only supported on X11");
            return;
        }
        if (args.Length == 0) {
            PrintHelp();
            return;
        }
        switch (args[0].ToLower())
        {
            case "server":
                {
                    BuffclipServer server = InitServer();    // Initiates server at 0.0.0.0:4443 Consider adjusting this via Parameters
                    HotkeyManager.ListenForKeyPress(server); // Waits for KeyPress/KeyRelease Events
                    break;
                }

            case "client":
                {
                    if (args.Length != 2) {
                        Console.WriteLine("Usage: buffclip client <ip>");
                        return;
                    }
                    BuffclipClient client = InitClient();    // Initiates client and listens for Server packet
                    HotkeyManager.ListenForKeyPress(client);
                    break;
                }

            default:
                PrintHelp();
                break;
        }
    }
}


