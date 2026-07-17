

static class Globals
{
        static int NumberOfBuffers = 2;
        public static BuffersManager BuffersManager = new BuffersManager(NumberOfBuffers);  // Initiate Buffers;
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


    static void InitiateNetworkListener()
    {
        BuffclipServer server = new BuffclipServer("0.0.0.0", 4443);        // Default values for now
        Thread thread = new Thread(server.StartServer);     // Starts server and handles client connections
        thread.IsBackground = true;
        thread.Start();
    }

    
    static void Main(string[] args)
    {
        if (args.Length == 0) {
            PrintHelp();
            return;
        }
        switch (args[0].ToLower())
        {
            case "server":
                {
                    InitiateNetworkListener(); // Initiates server at 0.0.0.0:4444 Consider adjusting this via Parameters
                    HotkeyManager.ListenForKeyPress(); // Waits for KeyPress/KeyRelease Events
                    break;
                }

            case "client":
                {
                    if (args.Length != 2) {
                        Console.WriteLine("Usage: buffclip client <ip>");
                        return;
                    }
                    BuffclipClient client = new BuffclipClient("192.168.1.47", 4443);
                    client.Connect();
                    client.SendFullSyncRequest();
                    client.GetFullSyncResponse();
                    break;
                }

            default:
                PrintHelp();
                break;
        }
    }
}


