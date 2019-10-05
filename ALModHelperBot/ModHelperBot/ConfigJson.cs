﻿// THIS FILE IS A PART OF EMZI0767'S BOT EXAMPLES
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

using Newtonsoft.Json;

namespace ALModHelperBot.ModHelperBot
{
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