// THIS FILE IS A PART OF EMZI0767'S BOT EXAMPLES
//
// --------
// 
// Copyright 2017 Emzi0767
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// --------
//
// This is a basic example. It shows how to set up a project and connect to 
// Discord, as well as perform some simple tasks.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using System.Net;
using System.Text.RegularExpressions;

namespace Discord_AL_RoleBot
{
    public class Program
    {
        public DiscordClient Client { get; set; }
        public DiscordGuild discordGuild { get; set; }
        public ObservableCollection<DiscordMember> Members { get; private set; }

        private string Roll20Game { get; set; }
        private string CommandPrefix { get; set; }
        private ulong DiscordServer { get; set; }
        private DiscordGuild Guild { get; set; }
        private string AdventurerRoleName { get; set; }
        private DiscordRole AdventurerRole { get; set; }
        private DiscordRole[] WhitelistRoleCommandUse { get; set; }
        private string[] WhitelistRoleCommandUseName { get; set; }
        private Cache Cache { get; set; }

        public static void Main(string[] args)
        {
            // since we cannot make the entry method asynchronous,
            // let's pass the execution to asynchronous code
            var prog = new Program();
            prog.RunBotAsync().GetAwaiter().GetResult();
        }

        public async Task RunBotAsync()
        {
            // first, let's load our configuration file
            var json = "";
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            var cfg = new DiscordConfiguration
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = true
            };

            Roll20Game = cfgjson.Roll20Game;
            DiscordServer = cfgjson.DiscordServer;
            CommandPrefix = cfgjson.CommandPrefix;
            AdventurerRoleName = cfgjson.AdventurerRoleName;
            WhitelistRoleCommandUseName = cfgjson.WhitelistRoleNames;

            // then we want to instantiate our client
            this.Client = new DiscordClient(cfg);

            // If you are on Windows 7 and using .NETFX, install 
            // DSharpPlus.WebSocket.WebSocket4Net from NuGet,
            // add appropriate usings, and uncomment the following
            // line
            //this.Client.SetWebSocketClient<WebSocket4NetClient>();

            // If you are on Windows 7 and using .NET Core, install 
            // DSharpPlus.WebSocket.WebSocket4NetCore from NuGet,
            // add appropriate usings, and uncomment the following
            // line
            //this.Client.SetWebSocketClient<WebSocket4NetCoreClient>();

            // If you are using Mono, install 
            // DSharpPlus.WebSocket.WebSocketSharp from NuGet,
            // add appropriate usings, and uncomment the following
            // line
            //this.Client.SetWebSocketClient<WebSocketSharpClient>();

            // if using any alternate socket client implementations, 
            // remember to add the following to the top of this file:
            //using DSharpPlus.Net.WebSocket;

            // next, let's hook some events, so we know
            // what's going on
            this.Client.Ready += this.Client_Ready;
            this.Client.GuildAvailable += this.Client_GuildAvailable;
            this.Client.ClientErrored += this.Client_ClientError;
            this.Client.MessageCreated += this.Client_MessageCreated;

            Cache = new Cache() { LastCacheUpdate = DateTimeOffset.MinValue, Players = new List<NonAdventurer>(), Roll20Users = new List<Roll20User>(), CurrentlyUpdating = false };

            // finally, let's connect and log in
            await this.Client.ConnectAsync();

            // and this is to prevent premature quitting
            await Task.Delay(-1);
        }

        private Task Client_Ready(ReadyEventArgs e)
        {
            // let's log the fact that this event occured
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "ExampleBot", "Client is ready to process events.", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done

            return Task.CompletedTask;
        }

        private Task Client_MessageCreated(MessageCreateEventArgs e)
        {
            Task<DiscordMember> cmdMember = Guild.GetMemberAsync(e.Author.Id);
            cmdMember.Wait();

            if (cmdMember.Result == null)
                return Task.CompletedTask;
            
            if (!cmdMember.Result.Roles.Select(x => x).Intersect(WhitelistRoleCommandUse).Any())
                return Task.CompletedTask;

            if (e.Channel.IsPrivate && e.Message.Content.StartsWith(CommandPrefix))
            {
                string outS;
                int line = 0;
                string command = e.Message.Content.Remove(0, CommandPrefix.Length);
                if (command != "update" && Cache.LastCacheUpdate == DateTimeOffset.MinValue)
                {
                    e.Channel.SendMessageAsync("Bitte führe zuerst !update aus damit die Listen der Spieler erstell werden.").Wait();
                    return Task.CompletedTask;
                }

                switch (command)
                {
                    case "update":
                        if (Cache.CurrentlyUpdating)
                        {
                            e.Channel.SendMessageAsync("Listen werden bereits geupdated, bitte warten...");
                            while (Cache.CurrentlyUpdating) { }
                            e.Channel.SendMessageAsync("Listen update abgeschlossen.");
                            return Task.CompletedTask;
                        }

                        e.Channel.SendMessageAsync("Update der Listen aller Spieler wird begonnen, bitte warten...").Wait();
                        UpdatePlayers(e.Channel);
                        
                        return Task.CompletedTask;

                    case "listNonAdv":
                        outS = "Liste aller nicht abenteurer:\n";
                        foreach (NonAdventurer p in Cache.Players)
                            outS += $"Player: {p.DisplayName}\nLast Checked: {p.LastSeen.ToString()} UTC\nIn Roll20: {(p.IsInRoll20 ? ":white_check_mark:" : ":x:")}\n---\n";
                        e.Channel.SendMessageAsync(outS);
                        return Task.CompletedTask;

                    case "listR20":
                        outS = "Liste aller Roll20 Mitglieder:\n```";
                        foreach (Roll20User p in Cache.Roll20Users)
                        {
                            outS += $"Player: {p.DisplayName}\nFirst Seen: {p.FirstSeen.ToString()} UTC\n---\n";
                            line++;

                            if (line > 10)
                            {
                                outS += "```";
                                e.Channel.SendMessageAsync(outS);
                                line = 0;
                                outS = "```";
                            }
                        }
                        if (line <= 10)
                        {
                            outS += "```";
                            e.Channel.SendMessageAsync(outS);
                        }

                        return Task.CompletedTask;

                    case "status":
                        e.Channel.SendMessageAsync($"Letztes Cache Update war am {Cache.LastCacheUpdate.ToString()}");
                        return Task.CompletedTask;

                    case "help":
                        e.Channel.SendMessageAsync("Das sind die Befehle die du nutzen kannst:\n------------------\n" +
                            "!help: Zeigt diese Nachricht an.\n" +
                            "!status: Zeigt wann die Listen als letztes geupdated wurden.\n" +
                            "!update: Updatet die interne Nicht Abenteurer Liste und Roll20 Liste.\n" +
                            "!listNonAdv: Zeigt die komplette Nicht Abenteurer Liste.\n" +
                            "!listR20: Zeigt die komplette Roll20 Spielerliste. VORSICHT SPAM!\n" +
                            "!find <name>: Zeigt alle Nicht Adventurer und Roll20 Spieler mit EXAKT diesem Namen.\n" +
                            "!findp <name>: Zeigt alle Nicht Adventurer und Roll20 Spieler die diesem Namen beinhalten.\n" +
                            "\n").Wait();
                        return Task.CompletedTask;
                }

                if (command.StartsWith("find "))
                {
                    string name = command.Remove(0, 5);
                    line = 0;
                    outS = "Nicht abenteurer mit diesem Namen:\n";                 
                    foreach (NonAdventurer p in Cache.Players.Where(p => p.DisplayName.Trim().ToLower() == name.Trim().ToLower()))
                    {
                        outS += $"Player: {p.DisplayName}\nLast Checked: {p.LastSeen.ToString()} UTC\nIn Roll20: {(p.IsInRoll20 ? ":white_check_mark:" : ":x:")}\n---\n";
                        line++;

                        if (line > 10)
                        {
                            e.Channel.SendMessageAsync(outS);
                            line = 0;
                            outS = "";
                        }
                    }
                    
                    outS += "Roll20 User mit diesem Namen:\n";
                    foreach (Roll20User p in Cache.Roll20Users.Where(p => p.DisplayName.Trim().ToLower() == name.Trim().ToLower()))
                    {
                        outS += $"```Player: {p.DisplayName}\nFirst Seen: {p.FirstSeen.ToString()} UTC```\n";
                        line++;

                        if (line > 10)
                        {
                            e.Channel.SendMessageAsync(outS);
                            line = 0;
                            outS = "";
                        }
                    }

                    if (outS.Length > 0)
                    {
                        e.Channel.SendMessageAsync(outS);
                    }

                    return Task.CompletedTask;
                }

                if (command.StartsWith("findp "))
                {
                    string name = command.Remove(0, 6);
                    line = 0;
                    outS = "Nicht abenteurer mit ähnlichen Namen:\n";
                    foreach (NonAdventurer p in Cache.Players.Where(p => p.DisplayName.Trim().ToLower().Contains(name.Trim().ToLower())))
                    {
                        outS += $"{(p.DisplayName.Trim().ToLower() == name.Trim().ToLower() ? ":star:" : "")}Player: {p.DisplayName}\nLast Checked: {p.LastSeen.ToString()} UTC\nIn Roll20: {(p.IsInRoll20 ? ":white_check_mark:" : ":x:")}\n---\n";
                        line++;

                        if (line > 10)
                        {
                            e.Channel.SendMessageAsync(outS);
                            line = 0;
                            outS = "";
                        }
                    }
                        
                    outS += "Roll20 User mit ähnlichen Namen:\n";
                    foreach (Roll20User p in Cache.Roll20Users.Where(p => p.DisplayName.Trim().ToLower().Contains(name.Trim().ToLower())))
                    {
                        outS += $"{(p.DisplayName.Trim().ToLower() == name.Trim().ToLower() ? ":star:" : "")}```Player: {p.DisplayName}\nFirst Seen: {p.FirstSeen.ToString()} UTC```\n";
                        line++;

                        if (line > 10)
                        {
                            e.Channel.SendMessageAsync(outS);
                            line = 0;
                            outS = "";
                        }
                    }
                        

                    if (outS.Length > 0)
                    {
                        e.Channel.SendMessageAsync(outS);
                    }

                    return Task.CompletedTask;
                }
            }

            e.Channel.SendMessageAsync("Unbekannter Befehl, bitte nutze !help um eine Liste von Befehlen zu erhalten.");

            return Task.CompletedTask;
        }

        private async Task UpdatePlayers(DiscordChannel channel)
        {
            Cache.CurrentlyUpdating = true;
            IReadOnlyList<DiscordMember> discordMembers = await Guild.GetAllMembersAsync();
            List<Roll20User> r20Result = await CrawlRoll20();
            Cache.LastCacheUpdate = DateTimeOffset.Now;

            foreach (Roll20User r20 in r20Result)
            {
                if (!Cache.Roll20Users.Exists(x => x.UID == r20.UID))
                {
                    Cache.Roll20Users.Add(r20);
                }                
            }

            foreach (Roll20User r20 in Cache.Roll20Users)
            {
                if (!r20Result.Exists(x => x.UID == r20.UID))
                {
                    Cache.Roll20Users.Remove(r20);
                }
            }

            Cache.Players = new List<NonAdventurer>();
            
            foreach (DiscordMember member in discordMembers.Where(p => !p.Roles.Contains(AdventurerRole)))
            {
                Cache.Players.Add(new NonAdventurer() { DisplayName = member.DisplayName, LastSeen = DateTimeOffset.Now, IsInRoll20 = (Cache.Roll20Users.Where(p => p.DisplayName.Trim().ToLower() == member.DisplayName.Trim().ToLower()).ToList().Count > 0) });
            }

            Cache.CurrentlyUpdating = false;

            if (channel != null)
                channel.SendMessageAsync("Update abgeschlossen.").Wait();
        }

        private async Task<List<Roll20User>> CrawlRoll20()
        {
            List<Roll20User> nameList = new List<Roll20User>();

            try
            {
                WebRequest request = WebRequest.Create(Roll20Game);
                WebResponse response = (HttpWebResponse)await Task.Factory
                                        .FromAsync<WebResponse>(request.BeginGetResponse,
                                        request.EndGetResponse,
                                        null);
                Stream data = response.GetResponseStream();
                string html = String.Empty;
                using (StreamReader sr = new StreamReader(data))
                {
                    html = sr.ReadToEnd();
                }

                html = html.Replace("\n", String.Empty);
                html = html.Replace("\t", String.Empty);
                html = html.Replace("\r", String.Empty);

                

                while (true)
                {
                    int pcListing = html.IndexOf("<div class='pclisting'><a href='/users/");
                    if (pcListing != -1)
                    {
                        //int uidbegin = html.IndexOf("\">", pcListing + 40) + 1;
                        html = html.Remove(0, pcListing + 40);
                        int uidEnd = html.IndexOf("'>");
                        string uid = html.Substring(0, uidEnd);
                        html = html.Remove(0, uidEnd + 2);
                        int nameBegin = html.IndexOf(">", html.IndexOf("<img class="))+1;
                        int nameEnd = html.IndexOf("<", nameBegin);
                        string name = html.Substring(nameBegin, nameEnd-nameBegin);
                        html = html.Remove(0, nameEnd+1);


                        nameList.Add(new Roll20User() {DisplayName = name, FirstSeen = DateTimeOffset.Now, UID = uid });
                    }
                    else
                    {
                        break;
                    }
                }
                
            }
            catch (ArgumentNullException)
            {
                // connection lost?
            }

            return nameList;
        }

        private Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            if (e.Guild.Id == DiscordServer)
            {
                Task<DiscordGuild> g = Client.GetGuildAsync(DiscordServer);
                g.Wait();
                Guild = g.Result;
                AdventurerRole = Guild.Roles.First(r => r.Name == AdventurerRoleName);
                WhitelistRoleCommandUse = Guild.Roles.Where(r => WhitelistRoleCommandUseName.Contains(r.Name)).ToArray();

                UpdatePlayers(null).Wait();
            }
           

            // let's log the name of the guild that was just
            // sent to our client
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "ExampleBot", $"Guild available: {e.Guild.Name}", DateTime.Now);
            discordGuild = e.Guild;
            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            // let's log the details of the error that just 
            // occured in our client
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "ExampleBot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }
    }

    public struct NonAdventurer
    {
        public string DisplayName;
        public DateTimeOffset LastSeen;
        public bool IsInRoll20;
    }

    public struct Roll20User
    {
        public string DisplayName;
        public DateTimeOffset FirstSeen;
        public string UID;
    }

    public class Cache
    {
        public List<NonAdventurer> Players { get; set; }
        public List<Roll20User> Roll20Users { get; set; }
        public DateTimeOffset LastCacheUpdate { get; set; }
        public bool CurrentlyUpdating { get; set; }
    }

    // this structure will hold data from config.json
    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("prefix")]
        public string CommandPrefix { get; private set; }

        [JsonProperty("roll20Game")]
        public string Roll20Game { get; private set; }

        [JsonProperty("discordServer")]
        public ulong DiscordServer { get; private set; }

        [JsonProperty("adventurerRoleName")]
        public string AdventurerRoleName { get; private set; }

        [JsonProperty("whitelistRoleNames")]
        public string[] WhitelistRoleNames { get; private set; }
    }
}