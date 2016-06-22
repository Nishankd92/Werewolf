﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Werewolf_Control.Handler;
using Werewolf_Control.Models;

namespace Werewolf_Control.Helpers
{
    internal static class Bot
    {
#if DEBUG
        internal static string TelegramAPIKey = Internal.Default.TelegramAPIDebug;
#else
        internal static string TelegramAPIKey = Internal.Default.TelegramAPIProduction;
#endif
        public static HashSet<Node> Nodes = new HashSet<Node>();
        public static readonly Client Api = new Client(TelegramAPIKey);
        public static User Me;
        public static DateTime StartTime = DateTime.UtcNow;
        public static bool Running = true;
        public static long CommandsReceived = 0;
        public static long MessagesReceived = 0;
        public static long TotalPlayers = 0;
        public static long TotalGames = 0;
        public static Random R = new Random();

        internal static string RootDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
        internal delegate void ChatCommandMethod(Update u, string[] args);
        internal static List<Command> Commands = new List<Command>();
        internal static string LanguageDirectory => Path.GetFullPath(Path.Combine(RootDirectory, @"..\Languages"));
        internal static string TempLanguageDirectory => Path.GetFullPath(Path.Combine(RootDirectory, @"..\TempLanguageFiles"));
        public static void Initialize()
        {
            //load the commands list
            foreach (var m in typeof(Werewolf_Control.Commands).GetMethods())
            {
                var c = new Command();
                foreach (var a in m.GetCustomAttributes(true))
                {
                    if (a is Attributes.Command)
                    {
                        var ca = a as Attributes.Command;
                        c.Blockable = ca.Blockable;
                        c.DevOnly = ca.DevOnly;
                        c.GlobalAdminOnly = ca.GlobalAdminOnly;
                        c.GroupAdminOnly = ca.GroupAdminOnly;
                        c.Trigger = ca.Trigger;
                        c.Method = (ChatCommandMethod) Delegate.CreateDelegate(typeof(ChatCommandMethod), m);
                        c.InGroupOnly = ca.InGroupOnly;
                        Commands.Add(c);
                    }
                }
            }

            Api.UpdateReceived += UpdateHandler.UpdateReceived;
            Api.CallbackQueryReceived += UpdateHandler.CallbackReceived;
            Api.ReceiveError += ApiOnReceiveError;
            Me = Api.GetMe().Result;
            //wait for a node to connect...
            //#if !DEBUG
            //            while (Nodes.Count == 0)
            //            {
            //                Thread.Sleep(100);
            //            }
            //#endif
            StartTime = DateTime.UtcNow;
            //now we can start receiving
            Api.StartReceiving();
            //new Task(SendOnline).Start();
        }

        private static void ApiOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            if (!Api.IsReceiving)
            {
                Api.StartReceiving();
            }
            var e = receiveErrorEventArgs.ApiRequestException;
            Program.Log($"{e.ErrorCode} - {e.Message}\n{e.Source}");
        }

        private static void Reboot()
        {
            Running = false;
            Program.Running = false;
            Process.Start(Assembly.GetExecutingAssembly().Location);
            Environment.Exit(4);

        }

        public static void SendOnline()
        {
#if !DEBUG
            var ids = DBHelper.GetNotifyGroups().ToList();
            foreach (var id in ids)
            {
                Api.SendTextMessage(id, "Werewolf Bot 2.0 online.  Join the dev channel for live updates: https://telegram.me/werewolfdev\nTo disable this message or change other settings, use /config (admin only)");
            }
#else
            Api.SendTextMessage(Settings.MainChatId, "Bot 2.0 Test online (I should be named Mr. Spammy)");
#endif
        }

        //TODO this needs to be an event
        public static void NodeConnected(Node n)
        {
#if DEBUG
            Api.SendTextMessage(Settings.MainChatId, $"Node connected with guid {n.ClientId}");
#endif
        }

        //TODO this needs to be an event as well
        public static void Disconnect(this Node n)
        {
#if DEBUG
            Api.SendTextMessage(Settings.MainChatId, $"Node disconnected with guid {n.ClientId}");
#endif
            foreach (var g in n.Games)
            {
                Api.SendTextMessage(g.GroupId, $"Something went wrong, and this node has shut down.");
            }
            Nodes.Remove(n);
            n = null;
        }

        /// <summary>
        /// Gets the node with the least number of current games
        /// </summary>
        /// <returns>Best node, or null if no nodes</returns>
        public static Node GetBestAvailableNode()
        {
            //make sure we remove bad nodes first
            foreach (var n in Nodes.Where(x => x.TcpClient.Connected == false).ToList())
                Nodes.Remove(n);
            return Nodes.Where(x => x.ShuttingDown == false && x.CurrentGames < Settings.MaxGamesPerNode).OrderBy(x => x.CurrentGames).FirstOrDefault(); //if this is null, there are no nodes
        }

        internal static void Send(string message, long id, bool clearKeyboard = false, ReplyKeyboardMarkup customMenu = null)
        {
            //message = message.Replace("`",@"\`");
            if (clearKeyboard)
            {
                var menu = new ReplyKeyboardHide { HideKeyboard = true };
                Api.SendTextMessage(id, message, replyMarkup: menu, disableWebPagePreview: true);
            }
            else if (customMenu != null)
            {
                Api.SendTextMessage(id, message, replyMarkup: customMenu, disableWebPagePreview: true);
            }
            else
            {
                Api.SendTextMessage(id, message, disableWebPagePreview: true);
            }

        }

        internal static GameInfo GetGroupNodeAndGame(long id)
        {
            var node = Nodes.ToList().FirstOrDefault(n => n.Games.Any(g => g.GroupId == id))?.Games.FirstOrDefault(x => x.GroupId == id);
            if (node == null)
                node = Nodes.ToList().FirstOrDefault(n => n.Games.Any(g => g.GroupId == id))?.Games.FirstOrDefault(x => x.GroupId == id);
            if (node == null)
                node = Nodes.ToList().FirstOrDefault(n => n.Games.Any(g => g.GroupId == id))?.Games.FirstOrDefault(x => x.GroupId == id);
            return node;
        }
    }
}