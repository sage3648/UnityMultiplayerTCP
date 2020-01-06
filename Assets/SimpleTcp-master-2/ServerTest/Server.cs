﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleTcp;

namespace ServerTestNetCore
{
    class Program
    {
        static string _ListenerIp;
        static int _ListenerPort;
        static bool _Ssl;
        static string _PfxFilename = null;
        static string _PfxPassword = null;

        static TcpServer _Server;
        static bool _RunForever = true;

        static void Main(string[] args)
        {
            _ListenerIp = InputString("Listener IP:", "127.0.0.1", false);
            _ListenerPort = InputInteger("Listener Port:", 9000, true, false);
            _Ssl = InputBoolean("Use SSL:", false);

            if (_Ssl)
            {
                _PfxFilename = InputString("PFX Certificate File:", "simpletcp.pfx", false);
                _PfxPassword = InputString("PFX File Password:", "simpletcp", false);
            }

            _Server = new TcpServer(_ListenerIp, _ListenerPort, _Ssl, _PfxFilename, _PfxPassword);

            _Server.ClientConnected = ClientConnected;
            _Server.ClientDisconnected = ClientDisconnected;
            _Server.DataReceived = DataReceived;
            _Server.IdleClientTimeoutSeconds = 5;
            _Server.Debug = false;
            _Server.MutuallyAuthenticate = false;
            _Server.AcceptInvalidCertificates = true;
            _Server.Start();

            while (_RunForever)
            {
                string userInput = InputString("Command [? for help]:", null, false);
                switch (userInput)
                {
                    case "?":
                        Menu();
                        break;
                    case "q":
                    case "Q":
                        _RunForever = false;
                        break;
                    case "c":
                    case "C":
                    case "cls":
                        Console.Clear();
                        break;
                    case "list":
                        ListClients();
                        break;
                    case "send":
                        Send();
                        break; 
                    case "remove":
                        Console.Write("IP:Port: ");
                        string ipPort = Console.ReadLine();
                        _Server.DisconnectClient(ipPort);
                        break; 
                    case "dispose":
                        _Server.Dispose();
                        break;
                }
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task ClientConnected(string ipPort)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("[" + ipPort + "] client connected");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task ClientDisconnected(string ipPort, DisconnectReason reason)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("[" + ipPort + "] client disconnected: " + reason.ToString());
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task DataReceived(string ipPort, byte[] data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("[" + ipPort + "]: " + Encoding.UTF8.GetString(data));
        }

        static void Menu()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine(" ?            Help, this menu");
            Console.WriteLine(" q            Quit");
            Console.WriteLine(" cls          Clear the screen");
            Console.WriteLine(" list         List connected clients");
            Console.WriteLine(" send         Send a message to a client");
            Console.WriteLine(" remove       Disconnect client");
            Console.WriteLine(" dispose      Dispose of the server");
            Console.WriteLine("");
        }

        static void ListClients()
        {
            List<string> clients = _Server.GetClients().ToList();
            if (clients != null && clients.Count > 0)
            {
                foreach (string curr in clients) Console.WriteLine(curr);
            }
            else Console.WriteLine("None");
        }

        static void Send()
        {
            string clientIp = InputString("Client IP:port:", null, true);
            if (!String.IsNullOrEmpty(clientIp))
            {
                string data = InputString("Data:", "Hello!", true);
                if (!String.IsNullOrEmpty(data))
                {
                    _Server.Send(clientIp, Encoding.UTF8.GetBytes(data));
                }
            }
        }

        static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (String.IsNullOrEmpty(userInput))
            {
                if (yesDefault) return true;
                return false;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                if (
                    (String.Compare(userInput, "n") == 0)
                    || (String.Compare(userInput, "no") == 0)
                   )
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (
                    (String.Compare(userInput, "y") == 0)
                    || (String.Compare(userInput, "yes") == 0)
                   )
                {
                    return true;
                }

                return false;
            }
        }

        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }

        static List<string> InputStringList(string question, bool allowEmpty)
        {
            List<string> ret = new List<string>();

            while (true)
            {
                Console.Write(question);

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (ret.Count < 1 && !allowEmpty) continue;
                    return ret;
                }

                ret.Add(userInput);
            }
        }

        static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                int ret = 0;
                if (!Int32.TryParse(userInput, out ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
        }
    }
}
