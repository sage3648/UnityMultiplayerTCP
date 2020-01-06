﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleTcp;

namespace ClientTestNetCore
{
    class Program
    {
        static string _ServerIp;
        static int _ServerPort;
        static bool _Ssl;
        static string _PfxFilename = null;
        static string _PfxPassword = null;

        static TcpClient _Client;
        static bool _RunForever = true;

        static void Main(string[] args)
        {
            _ServerIp = InputString("Listener IP:", "127.0.0.1", false);
            _ServerPort = InputInteger("Listener Port:", 9000, true, false);
            _Ssl = InputBoolean("Use SSL:", false);

            if (_Ssl)
            {
                _PfxFilename = InputString("PFX Certificate File:", "simpletcp.pfx", false);
                _PfxPassword = InputString("PFX File Password:", "simpletcp", false);
            }

            _Client = new TcpClient(_ServerIp, _ServerPort, _Ssl, _PfxFilename, _PfxPassword);

            _Client.Connected = Connected;
            _Client.Disconnected = Disconnected;
            _Client.DataReceived = DataReceived;
            _Client.Debug = false;
            _Client.MutuallyAuthenticate = false;
            _Client.AcceptInvalidCertificates = true;
            _Client.Connect();

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
                    case "send":
                        Send();
                        break;
                    case "connected":
                        IsConnected();
                        break;
                    case "dispose":
                        _Client.Dispose();
                        break;
                }
            }
        }

        static void IsConnected()
        {
            Console.WriteLine("Connected: " + _Client.IsConnected);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task Connected()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("*** Server connected");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task Disconnected()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("*** Server disconnected"); 
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static async Task DataReceived(byte[] data)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("[" + _ServerIp + ":" + _ServerPort + "] " + Encoding.UTF8.GetString(data));
        }

        static void Menu()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine(" ?            Help, this menu");
            Console.WriteLine(" q            Quit");
            Console.WriteLine(" cls          Clear the screen");
            Console.WriteLine(" send         Send a message to the server");
            Console.WriteLine(" connected    Display if the client is connected to the server");
            Console.WriteLine(" dispose      Dispose of the client");
            Console.WriteLine("");
        }

        static void Send()
        {
            string data = InputString("Data:", "Hello!", true);
            if (!String.IsNullOrEmpty(data))
            {
                _Client.Send(Encoding.UTF8.GetBytes(data));
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
