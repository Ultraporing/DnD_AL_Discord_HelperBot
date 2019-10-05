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

namespace ALModHelperBot.ModHelperBot
{
    public class Bot
    {
        public DiscordClient Client { get; set; }
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

        //private Dictionary<ulong, Task<SnowflakeObject>> CurrentUsersWaitingForCmdCompletion = new Dictionary<ulong, Task<SnowflakeObject>>();

        public async Task RunBotAsync()
        {
            var json = "";
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

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

            this.Client = new DiscordClient(cfg);

            this.Client.Ready += this.Client_Ready;
            this.Client.GuildAvailable += this.Client_GuildAvailable;
            this.Client.ClientErrored += this.Client_ClientError;
            this.Client.MessageCreated += this.Client_MessageCreatedAsync;
            
            Cache = new Cache() { LastCacheUpdate = DateTimeOffset.MinValue, Players = new List<NonAdventurer>(), Roll20Users = new List<Roll20User>(), CurrentlyUpdating = false };


            // finally, let's connect and log in
            await this.Client.ConnectAsync();

            // and this is to prevent premature quitting
            await Task.Delay(-1);
        }

        private Task Client_Ready(ReadyEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "ExampleBot", "Client is ready to process events.", DateTime.Now);

            return Task.CompletedTask;
        }

        private async Task<Task> AsyncMessageEvent(MessageCreateEventArgs e)
        {
            Task<DiscordMember> cmdMember = Guild.GetMemberAsync(e.Author.Id);
            cmdMember.Wait();

            if (cmdMember.Result == null)
                return Task.CompletedTask;

            if (!cmdMember.Result.Roles.Select(x => x).Intersect(WhitelistRoleCommandUse).Any())
                return Task.CompletedTask;

            if (e.Channel.IsPrivate && e.Message.Content.StartsWith(CommandPrefix))
            {                
                int line = 0;
                string command = e.Message.Content.Remove(0, CommandPrefix.Length);
                if (command != "update" && Cache.LastCacheUpdate == DateTimeOffset.MinValue)
                {
                    e.Channel.SendMessageAsync("Bitte führe zuerst !update aus damit die Listen der Spieler erstell werden.").Wait();
                    return Task.CompletedTask;
                }

                string outS = $"```css\n!{command}:\n```";
                switch (command)
                {
                    case "update":
                        if (Cache.CurrentlyUpdating)
                        {
                            outS += "Listen werden bereits geupdated, bitte warten...";
                            await e.Channel.SendMessageAsync(outS);
                            while (Cache.CurrentlyUpdating) { }
                            await e.Channel.SendMessageAsync("Listen update abgeschlossen.");
                            return Task.CompletedTask;
                        }

                        outS += "Update der Listen aller Spieler wird begonnen, bitte warten...";
                        await e.Channel.SendMessageAsync(outS);

                        await UpdatePlayers(e.Author, e.Channel);

                        return Task.CompletedTask;

                    case "listNonAdv":
                        outS += "Liste aller User ohne Abenteurer-Rolle:\n";
                        foreach (NonAdventurer p in Cache.Players)
                            outS += $"Player: {p.DisplayName}\nLast Checked: {p.LastSeen.ToString()} UTC\nIn Roll20: {(p.IsInRoll20 ? ":white_check_mark:" : ":x:")}\n---\n";
                        await e.Channel.SendMessageAsync(outS);
                        return Task.CompletedTask;

                    case "listR20":
                        outS += "Liste aller Roll20 Mitglieder:\n```";
                        foreach (Roll20User p in Cache.Roll20Users)
                        {
                            outS += $"Player: {p.DisplayName}\nFirst Seen: {p.FirstSeen.ToString()} UTC\n---\n";
                            line++;

                            if (outS.Length >= 800)
                            {
                                outS += "```";
                                await e.Channel.SendMessageAsync(outS);
                                line = 0;
                                outS = "```";
                            }
                        }
                        if (line > 0)
                        {
                            outS += "```";
                            await e.Channel.SendMessageAsync(outS);
                        }
                        return Task.CompletedTask;

                    case "status":
                        outS += $"Letztes Cache Update war am {Cache.LastCacheUpdate.ToString()}";
                        await e.Channel.SendMessageAsync(outS);
                        return Task.CompletedTask;

                    case "help":
                        outS += "Das sind die Befehle die du nutzen kannst:\n------------------\n" +
                            "!help: Zeigt diese Nachricht an.\n" +
                            "!status: Zeigt wann die Listen als letztes geupdated wurden.\n" +
                            "!update: Updatet die interne Nicht Abenteurer Liste und Roll20 Liste.\n" +
                            "!listNonAdv: Zeigt die komplette Nicht Abenteurer Liste.\n" +
                            "!listR20: Zeigt die komplette Roll20 Spielerliste. VORSICHT SPAM!\n" +
                            "!find <name>: Zeigt alle Nicht Adventurer und Roll20 Spieler mit EXAKT diesem Namen.\n" +
                            "!findp <name>: Zeigt alle Nicht Adventurer und Roll20 Spieler die diesem Namen beinhalten.\n" +
                            "\n";
                        await e.Channel.SendMessageAsync(outS);
                        return Task.CompletedTask;
                }

                if (command.StartsWith("find "))
                {
                    string name = command.Remove(0, 5);
                    line = 0;
                    outS += "User ohne Abenteurer-Rolle mit diesem Namen:\n";
                    foreach (NonAdventurer p in Cache.Players.Where(p => p.DisplayName.Trim().ToLower() == name.Trim().ToLower()))
                    {
                        outS += $"Player: {p.DisplayName}\nLast Checked: {p.LastSeen.ToString()} UTC\nIn Roll20: {(p.IsInRoll20 ? ":white_check_mark:" : ":x:")}\n---\n";
                        line++;

                        if (outS.Length > 800)
                        {
                            await e.Channel.SendMessageAsync(outS);
                            line = 0;
                            outS = "";
                        }
                    }

                    outS += "Roll20 User mit diesem Namen:\n";
                    foreach (Roll20User p in Cache.Roll20Users.Where(p => p.DisplayName.Trim().ToLower() == name.Trim().ToLower()))
                    {
                        outS += $"```Player: {p.DisplayName}\nFirst Seen: {p.FirstSeen.ToString()} UTC```\n";
                        line++;

                        if (outS.Length > 800)
                        {
                            await e.Channel.SendMessageAsync(outS);
                            line = 0;
                            outS = "";
                        }
                    }

                    if (line > 0)
                    {
                        await e.Channel.SendMessageAsync(outS);
                    }

                    return Task.CompletedTask;
                }

                if (command.StartsWith("findp "))
                {
                    string name = command.Remove(0, 6);
                    line = 0;
                    outS += "User ohne Abenteurer-Rolle mit ähnlichem Namen:\n";
                    foreach (NonAdventurer p in Cache.Players.Where(p => p.DisplayName.Trim().ToLower().Contains(name.Trim().ToLower())))
                    {
                        outS += $"{(p.DisplayName.Trim().ToLower() == name.Trim().ToLower() ? ":star:" : "")}Player: {p.DisplayName}\nLast Checked: {p.LastSeen.ToString()} UTC\nIn Roll20: {(p.IsInRoll20 ? ":white_check_mark:" : ":x:")}\n---\n";
                        line++;

                        if (outS.Length > 800)
                        {
                            await e.Channel.SendMessageAsync(outS);
                            line = 0;
                            outS = "";
                        }
                    }

                    outS += "Roll20 User mit ähnlichen Namen:\n";
                    foreach (Roll20User p in Cache.Roll20Users.Where(p => p.DisplayName.Trim().ToLower().Contains(name.Trim().ToLower())))
                    {
                        outS += $"{(p.DisplayName.Trim().ToLower() == name.Trim().ToLower() ? ":star:" : "")}```Player: {p.DisplayName}\nFirst Seen: {p.FirstSeen.ToString()} UTC```\n";
                        line++;

                        if (outS.Length > 800)
                        {
                            await e.Channel.SendMessageAsync(outS);
                            line = 0;
                            outS = "";
                        }
                    }


                    if (line > 0)
                    {
                        await e.Channel.SendMessageAsync(outS);
                    }

                    return Task.CompletedTask;
                }
            }

            await e.Channel.SendMessageAsync("Unbekannter Befehl, bitte nutze !help um eine Liste von Befehlen zu erhalten.");

            return Task.CompletedTask;
        }

        private async Task<Task> Client_MessageCreatedAsync(MessageCreateEventArgs e)
        {
            return await AsyncMessageEvent(e);
        }

        private async Task UpdatePlayers(DiscordUser cmdCaller, DiscordChannel channel)
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
                        int nameBegin = html.IndexOf(">", html.IndexOf("<img class=")) + 1;
                        int nameEnd = html.IndexOf("<", nameBegin);
                        string name = html.Substring(nameBegin, nameEnd - nameBegin);
                        html = html.Remove(0, nameEnd + 1);


                        nameList.Add(new Roll20User() { DisplayName = name, FirstSeen = DateTimeOffset.Now, UID = uid });
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

                e.Client.DebugLogger.LogMessage(LogLevel.Info, "ALModHelperBot", $"Guild Member: {e.Guild.Name} and allows commands", DateTime.Now);
                UpdatePlayers(null, null).Wait();
                e.Client.DebugLogger.LogMessage(LogLevel.Info, "ALModHelperBot", $"Bot ready and accepts commands", DateTime.Now);
            }
            else
                e.Client.DebugLogger.LogMessage(LogLevel.Info, "ALModHelperBot", $"Guild Member: {e.Guild.Name}", DateTime.Now);

            return Task.CompletedTask;
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "ALModHelperBot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);
            return Task.CompletedTask;
        }
    }
}