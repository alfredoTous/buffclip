using System.CommandLine;
using System.Net;


// Global state for buffers
static class Globals
{
    public static BuffersManager BuffersManager = new BuffersManager(2);  // Initiate 2 Buffers
}


// ==============================================
// =============== Entry point ==================
// ==============================================

class Program
{
    static int Main(string[] args)
    {
        if (!PlatformGuard.CheckX11()) return 1;

        RootCommand cli = ArgParser.BuildCli();
        return cli.Parse(args).Invoke();
    }


    // ==== Mode launchers =========================

    public static void RunServerMode(string[] interfaces, int port, bool noDiscover)
    {
        Dictionary<IPAddress, IPAddress?> ListenAddresses = new Dictionary<IPAddress, IPAddress?>();

        foreach (string i in interfaces)
        {
            if (!IPAddress.TryParse(i, out IPAddress? ip))
                throw new Exception($"Invalid IP address format: {i}");

            IPAddress? mask = BuffclipServer.GetSubnetMask(ip);
            ListenAddresses.Add(ip, mask);
        }
        foreach (var (ip, mask) in ListenAddresses)
{
    Console.WriteLine($"Configured interface: {ip} Mask: {mask}");
}

        BuffclipServer server = InitServer(ListenAddresses, port);

        if (!noDiscover)
        {
            // Init udp listener for automatic client discovery
            Console.WriteLine("[i] Starting UDP listener on background for automatic client discovery");
            Thread thread = new Thread(server.StartDiscoveryListener);
            thread.IsBackground = true;
            thread.Start();
        }
        else
        {
            Console.WriteLine("[i] UDP discovery listener disabled");
        }

        HotkeyManager.ListenForKeyPress(server);
    }

    public static void RunLocalMode()
    {
        Console.WriteLine("[i] Starting in local-only mode...");
        LocalNetworkManager localManager = new LocalNetworkManager();
        HotkeyManager.ListenForKeyPress(localManager);
    }

    public static void RunClientMode(string ip, int port)
    {
        BuffclipClient client = InitClient(ip, port);
        HotkeyManager.ListenForKeyPress(client);
    }


    // ==== Initialisers ===========================

    private static BuffclipServer InitServer(Dictionary<IPAddress, IPAddress?> ListenAddresses, int port)
    {
        BuffclipServer server = new BuffclipServer(ListenAddresses, port);
        Thread thread = new Thread(server.Start);   // Starts server and handles client connections
        thread.IsBackground = true;
        thread.Start();
        return server;
    }

    private static BuffclipClient InitClient(string ip, int port)
    {
        BuffclipClient client = new BuffclipClient(ip, port);
        Thread thread = new Thread(client.Start);
        thread.IsBackground = true;
        thread.Start();
        return client;
    }
}


// ============================================
// ============= Platform guard ===============
// ============================================

static class PlatformGuard
{
    public static bool CheckX11()
    {
        if (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland")
        {
            Console.WriteLine("[-] Wayland session detected");
            Console.WriteLine("[-] Buffclip is currently only supported on X11");
            return false;
        }
        return true;
    }
}


// =============================================
// ============ CLI argument parser ============
// =============================================

static class ArgParser
{
    public static RootCommand BuildCli()
    {
        var rootCommand = new RootCommand("Buffclip - Network clipboard manager");

        rootCommand.Subcommands.Add(BuildServerCommand());
        rootCommand.Subcommands.Add(BuildClientCommand());
        rootCommand.Subcommands.Add(BuildLocalCommand());

        return rootCommand;
    }


    // ==== server =============================

    private static Command BuildServerCommand()
    {
        var cmd = new Command("server", "Start Buffclip in server mode.");

        var interfaceOption = new Option<string[]>("--interface")
        {
            Description = "Interface(s) to listen on. Can be specified multiple times.",
            DefaultValueFactory = _ => new[] { "0.0.0.0" }
        };
        interfaceOption.Aliases.Add("-i");

        var portOption = new Option<int>("--port")
        {
            Description = "Port to listen on.",
            DefaultValueFactory = _ => 4242
        };
        portOption.Aliases.Add("-p");

        var noDiscoverOption = new Option<bool>("--no-discover")
        {
            Description = "Disable the UDP broadcast listener used for automatic client discovery."
        };

        cmd.Options.Add(interfaceOption);
        cmd.Options.Add(portOption);
        cmd.Options.Add(noDiscoverOption);

        cmd.SetAction(parseResult =>
        {
            string[] interfaces = ParseInterfaces(parseResult.GetValue(interfaceOption));
            int      port       = parseResult.GetValue(portOption);
            bool     noDiscover = parseResult.GetValue(noDiscoverOption);

            if (!ConfirmIfBroadcastInterface(interfaces)) return;

            Program.RunServerMode(interfaces, port, noDiscover);
        });

        return cmd;
    }


    // ==== local ==================================

    private static Command BuildLocalCommand()
    {
        var cmd = new Command("local", "Start Buffclip without networking.");

        cmd.SetAction(_ => Program.RunLocalMode());

        return cmd;
    }


    // ==== client ==============================
    private static Command BuildClientCommand()
    {
        var cmd = new Command("client", "Connect to a Buffclip server.");

        var ipOption = new Option<string>("--ip")
        {
            Description = "IP address of the server to connect to. If omitted, Buffclip will attempt to find the server via broadcast."
        };

        var portOption = new Option<int>("--port")
        {
            Description = "Port of the server to connect to.",
            DefaultValueFactory = _ => 4242
        };
        portOption.Aliases.Add("-p");

        cmd.Options.Add(ipOption);
        cmd.Options.Add(portOption);

        cmd.SetAction(parseResult =>
        {
            string? ip   = parseResult.GetValue(ipOption);
            int?     port = parseResult.GetValue(portOption);
            
            if (string.IsNullOrEmpty(ip))
            {
                
                ip = BuffclipClient.DiscoverServerViaBroadcast(port ?? 4242);
                if (string.IsNullOrEmpty(ip))
                {
                    Console.WriteLine("[-] Could not find a server automatically. Please specify one with --ip.");
                    return;
                }
            }
            Program.RunClientMode(ip, port ?? 4242);
        });

        return cmd;
    }


    // ==== Helpers ========================================

    private static string[] ParseInterfaces(string[]? raw)
    {
        return (raw ?? new[] { "0.0.0.0" })
            .SelectMany(i => i.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(i => i.Trim())
            .ToArray();
    }

    private static bool ConfirmIfBroadcastInterface(string[] interfaces)
    {
        if (!interfaces.Contains("0.0.0.0")) return true;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[!] WARNING: Listening on '0.0.0.0' (all interfaces) exposes Buffclip to your entire network.");
        Console.WriteLine("    If you want to restrict this, use the '-i' / '--interface' option to specify a single or multiple interfaces (e.g. '-i 127.0.0.1').");
        Console.Write("    Are you on a secure/trusted network and sure you want to proceed? [y/N]: ");
        Console.ResetColor();

        string? response = Console.ReadLine();
        return response != null
            && (response.Trim().Equals("y",   StringComparison.OrdinalIgnoreCase)
            ||  response.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
