using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LoUAM
{
    class MainConsole
    {
        [DllImport("Kernel32")]
        static extern void AllocConsole();

        [DllImport("Kernel32")]
        static extern void FreeConsole();

        static LinkServer TheServer;

        static void PrintUsage()
        {
            Console.WriteLine($@"
LoUAM - {Assembly.GetExecutingAssembly().GetName().Version.ToString()}
https://github.com/Lady-Binary/LoUAM

usage: LoUAM.exe [--headless --http/https --port p --password w]

Options are:
    --headless      Start LoUAM server in headless mode, with no GUI.
    --http/https    Use HTTP or HTTPS. Default is HTTPS.
    --port          Port number that the server should listen to for incoming connections. Default is 4443.
    --password      Password used for authenticating LoUAM clients. Default is no password.

Headless mode is useful for running a server on a slim VM in the Cloud.
If no options are specified, LoUAM will start in regular GUI mode.

Example:
    LoUAM --headless --port 8443 --password s3cr3t
");
            Console.WriteLine("");
            Console.WriteLine();
            Console.WriteLine("");
            Console.WriteLine();
            Console.WriteLine();
        }

        public static async Task Start(string[] args)
        {
            bool Https = true;
            int Port = 4443;
            string Password = "";

            AllocConsole();

            if (args.Any(a =>
                    a.ToLower() == "/?" ||
                    a.ToLower() == "/help" ||
                    a.ToLower() == "-?" ||
                    a.ToLower() == "--help"))
            {
                PrintUsage();
            }
            else if (args.Any(a => a.ToLower() == "--headless"))
            {
                Console.CancelKeyPress += new ConsoleCancelEventHandler(OnCancelKeyPress);

                int arg = 0;
                while (arg < args.Length)
                {
                    switch (args[arg])
                    {
                        case "/h":
                        case "/headless":
                        case "--h":
                        case "--headless":
                            {
                                Console.WriteLine("Running in server headless mode.");
                            }
                            break;

                        case "/t":
                        case "/http":
                        case "--t":
                        case "--http":
                            {
                                Console.WriteLine("HTTP protocol selected.");
                                Https = false;
                            }
                            break;

                        case "/s":
                        case "/https":
                        case "--s":
                        case "--https":
                            {
                                Console.WriteLine("HTTPS protocol selected.");
                                Https = true;
                            }
                            break;


                        case "/p":
                        case "/port":
                        case "-p":
                        case "--port":
                            {
                                if (!int.TryParse(args[++arg], out Port))
                                {
                                    Console.WriteLine("Invalid port parameter, defaulting to 4443.");
                                    Port = 4443;
                                }
                                else
                                {
                                    Console.WriteLine($"Listening on port {Port}.");
                                }
                            }
                            break;
                        case "/w":
                        case "/password":
                        case "-w":
                        case "--password":
                            {
                                Password = args[++arg];
                                Console.WriteLine("Password set.");
                            }
                            break;
                        default:
                            PrintUsage();
                            Console.WriteLine("");
                            Console.WriteLine($"Unknown switch: {args[arg]}");
                            return;
                    }
                    arg++;
                }

                TheServer = new LinkServer(Https, Port, Password);
                TheServer.StartServer();

                Console.WriteLine("LoUAM Server started...");
                Console.WriteLine("Press CTRL+C at any time to stop LoUAM Server.");
                while (TheServer.ServerState == LinkServer.ServerStateEnum.Listening)
                    ;
                Console.WriteLine("LoUAM Server stopped!");
            }
            Console.WriteLine();
            Console.WriteLine("Press any key to close this window...");
            Console.ReadKey();
            FreeConsole();
        }

        static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine("\nStopping LoUAM Server...");
            TheServer.StopServer();
            args.Cancel = true;
        }
    }
}
