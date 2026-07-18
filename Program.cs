using System.CommandLine;

static class Globals
{
        public static BuffersManager BuffersManager = new BuffersManager(2);  // Initiate 2 Buffers;
}


class Program
{
    public static BuffclipServer InitServer(string[] listenAddresses, int port)
    {
        BuffclipServer server = new BuffclipServer(listenAddresses, port);
        Thread thread = new Thread(server.Start);                       // Starts server and handles client connections
        thread.IsBackground = true;
        thread.Start();
        return server;
    }

    public static BuffclipClient InitClient(string ip, int port)
    {
        BuffclipClient client = new BuffclipClient(ip, port);
        Thread thread = new Thread(client.Start);
        thread.IsBackground = true;
        thread.Start();
        return client;
    }

    public static string? DiscoverServerViaBroadcast(int port)
    {
        // TODO: Implement UDP broadcast to discover the server IP automatically.
        Console.WriteLine($"[i] Searching for server via broadcast on port {port}...");
        return null;
    }

    
    static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland") {
            Console.WriteLine("[-] Wayland session detected");
            Console.WriteLine("[-] Buffclip is currently only supported on X11");
            return 1;
        }

        return ArgParser.Create().Parse(args).Invoke();
    }
}


static class ArgParser
{
    public static RootCommand Create()
    {
        var rootCommand = new RootCommand("Buffclip - Network clipboard manager");

        // Server command
        var serverCommand = new Command("server", "Start Buffclip in server mode.");
        
        var serverInterfaceOption = new Option<string[]>("--interface")
        {
            Description = "Interface(s) to listen on. Can be specified multiple times.",
            DefaultValueFactory = _ => new[] { "0.0.0.0" }
        };
        serverInterfaceOption.Aliases.Add("-i");
        
        var serverPortOption = new Option<int>("--port")
        {
            Description = "Port to listen on.",
            DefaultValueFactory = _ => 4242
        };
        serverPortOption.Aliases.Add("-p");
        
        serverCommand.Options.Add(serverInterfaceOption);
        serverCommand.Options.Add(serverPortOption);
        
        serverCommand.SetAction(parseResult =>
        {
            var rawInterfaces = parseResult.GetValue(serverInterfaceOption) ?? new[] { "0.0.0.0" };
            var port = parseResult.GetValue(serverPortOption);
            
            var interfaces = rawInterfaces
                .SelectMany(i => i.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(i => i.Trim())
                .ToArray();

            if (interfaces.Contains("0.0.0.0"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[!] WARNING: Listening on '0.0.0.0' (all interfaces) exposes Buffclip to your entire network.");
                Console.WriteLine("    If you want to restrict this, use the '-i' / '--interface' option to specify a single or multiple interfaces (e.g. '-i 127.0.0.1').");
                Console.Write("    Are you on a secure/trusted network and sure you want to proceed? [y/N]: ");
                Console.ResetColor();
                string? response = Console.ReadLine();
                if (response == null || (!response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) && !response.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("[-] Operation cancelled by user. Server exiting.");
                    return;
                }
            }

            BuffclipServer server = Program.InitServer(interfaces, port);
            HotkeyManager.ListenForKeyPress(server);
        });

        // Local command
        var localCommand = new Command("local", "Start Buffclip in local-only mode (no network listening).");
        localCommand.SetAction(parseResult =>
        {
            Console.WriteLine("[i] Starting in local-only mode...");
            LocalNetworkManager localManager = new LocalNetworkManager();
            HotkeyManager.ListenForKeyPress(localManager);
        });

        // Client command
        var clientCommand = new Command("client", "Connect to a Buffclip server.");
        
        var clientIpOption = new Option<string>("--ip")
        {
            Description = "IP address of the server to connect to. If omitted, Buffclip will attempt to find the server via broadcast."
        };
        
        var clientPortOption = new Option<int>("--port")
        {
            Description = "Port of the server to connect to.",
            DefaultValueFactory = _ => 4242
        };
        clientPortOption.Aliases.Add("-p");

        clientCommand.Options.Add(clientIpOption);
        clientCommand.Options.Add(clientPortOption);

        clientCommand.SetAction(parseResult =>
        {
            var ip = parseResult.GetValue(clientIpOption);
            var port = parseResult.GetValue(clientPortOption);

            if (string.IsNullOrEmpty(ip))
            {
                ip = Program.DiscoverServerViaBroadcast(port);
                if (string.IsNullOrEmpty(ip))
                {
                    Console.WriteLine("[-] Could not find a server automatically. Please specify one with --ip.");
                    return;
                }
            }

            BuffclipClient client = Program.InitClient(ip, port);
            HotkeyManager.ListenForKeyPress(client);
        });

        rootCommand.Subcommands.Add(serverCommand);
        rootCommand.Subcommands.Add(localCommand);
        rootCommand.Subcommands.Add(clientCommand);

        return rootCommand;
    }
}


