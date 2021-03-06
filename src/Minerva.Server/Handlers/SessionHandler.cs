﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using AltV.Net;
using AltV.Net.Async;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Minerva.Server.DataAccessLayer.Services;
using Minerva.Server.Extensions;
using Microsoft.Extensions.Options;
using Minerva.Server.Core.Configuration;
using Minerva.Server.Core.Entities;
using Minerva.Server.Core.Contracts.Enums;
using Minerva.Server.Core.Contracts.Abstractions.ScriptStrategy;
using Minerva.Server.Core.Contracts.Models.Database;

namespace Minerva.Server.Handlers
{
    public class SessionHandler
        : IStartupSingletonScript
    {
        private static readonly Vector3 CharCreatorPedPosition = new Position(402.93603515625f, -996.7662963867188f, -99.00023651123047f);

        private readonly AccountService _accountService;
        private readonly CharacterService _characterService;
        private readonly ILogger<SessionHandler> _logger;
        private readonly DevelopmentOptions _devOptions;

        public Vector3 SpawnPoint { get; }

        public SessionHandler(
            ILogger<SessionHandler> logger,
            IOptions<GameOptions> gameOptions,
            IOptions<DevelopmentOptions> devOptions,
            AccountService accountService,
            CharacterService characterService)
        {
            _accountService = accountService;
            _characterService = characterService;
            _logger = logger;
            _devOptions = devOptions.Value;

            AltAsync.OnPlayerConnect += (player, reason) => OnPlayerConnect(player as ServerPlayer, reason);
            AltAsync.OnPlayerDisconnect += (player, reason) => OnPlayerDisconnect(player as ServerPlayer, reason);
            Alt.OnPlayerDead += (player, killer, weapon) => OnPlayerDead(player as ServerPlayer, killer, weapon);
            AltAsync.OnClient<ServerPlayer, string>("Login:Authenticate", OnLoginAuthenticateAsync);
            AltAsync.OnClient<ServerPlayer, int>("Session:RequestCharacterSpawn", OnRequestCharacterSpawnAsync);
            AltAsync.OnClient<ServerPlayer, string>("Session:CreateNewCharacter", OnCreateNewCharacterAsync);
            Alt.OnClient<ServerPlayer, Vector3>("RequestTeleport", OnRequestTeleport);
            
            SpawnPoint = new Vector3(
                gameOptions.Value.SpawnPointX, 
                gameOptions.Value.SpawnPointY, 
                gameOptions.Value.SpawnPointZ);
        }

        private async Task OnPlayerConnect(ServerPlayer player, string reason)
        {
            var uiUrl = "http://resource/client/html/index.html";

            if (_devOptions.DebugUI)
            {
                uiUrl = "http://localhost:8080/index.html";
            }

            var socialClubId = player.SocialClubId;
            var ip = player.Ip;

            _logger.LogInformation("Connection: SID {socialClub} with IP {ip}", socialClubId, ip);
            _logger.LogDebug("Requesting UI from {url}", uiUrl);

            if (!await _accountService.Exists(socialClubId))
            {
                await player.KickAsync("Du hast noch keinen Account auf diesem Server");
                return;
            }

            await player.SpawnAsync(Position.Zero);
            await Task.Delay(100);

            player.Emit("Session:Initialize", uiUrl);
        }

        private async Task OnPlayerDisconnect(ServerPlayer serverPlayer, string reason)
        {
            Character character;

            lock (serverPlayer)
            {
                if (serverPlayer.Exists && serverPlayer.IsLoggedIn)
                {
                    serverPlayer.Character.Position = serverPlayer.Position;
                    character = serverPlayer.Character;
                }
            }

            await _characterService.Update(serverPlayer.Character);
        }

        private void OnPlayerDead(ServerPlayer player, IEntity killer, uint weapon)
        {
            OnRequestCharacterSpawnAsync(player, player.Character.Id);
        }

        private async void OnLoginAuthenticateAsync(ServerPlayer player, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                player.Notify("Du musst ein gültiges Passwort eingeben!", NotificationType.Error);
                return;
            }

            var playerCharacters = new List<Dictionary<string, string>>();

            var account = await _accountService.Authenticate(
                player.SocialClubId,
                player.HardwareIdHash,
                player.HardwareIdExHash,
                password);

            if (account != null)
            {
                player.Account = account;

                foreach (var character in await _characterService.GetCharacters(account))
                {
                    var jsonObjSerialized = new Dictionary<string, string>();
                    jsonObjSerialized.Add("charId", character.Id.ToString());
                    jsonObjSerialized.Add("charName", character.Name);

                    playerCharacters.Add(jsonObjSerialized);
                }
            }

            // spawn player for char creator
            /*await player.SetDimensionAsync(100 + player.Id);
            await player.SetModelAsync(Alt.Hash("mp_m_freemode_01"));
            await player.SpawnAsync(new AltV.Net.Data.Position(402.93603515625f, -996.7662963867188f, -99.00023651123047f));*/

            await AltAsync.Do(() =>
            {
                player.Dimension = 100 + player.Id;
                player.Model = Alt.Hash("mp_m_freemode_01");
                player.Spawn(CharCreatorPedPosition, 0);
            });

            player.Emit("Login:Callback", player.IsLoggedIn, playerCharacters);
        }

        private async void OnRequestCharacterSpawnAsync(ServerPlayer player, int characterId)
        {
            var character = await _characterService.GetCharacter(characterId);

            if (character == null || character.AccountId != player.Account.SocialClubId)
            {
                player.Kick("Ungültiger Charakter");
                return;
            }

            player.Character = character;

            await SpawnCharacter(player);

            player.Emit("PlayerSpawned");
        }

        private async void OnCreateNewCharacterAsync(ServerPlayer player, string charCreationJson)
        {
            dynamic charCreationObj = JsonConvert.DeserializeObject<dynamic>(charCreationJson);

            string firstName = charCreationObj.firstName;
            string lastName = charCreationObj.lastName;
            string birthdayStr = charCreationObj.birthday;
            string genderIndex = charCreationObj.genderIndex;
            string appearanceParents = JsonConvert.SerializeObject(charCreationObj.appearanceParents);
            string appearanceFaceFeatures = JsonConvert.SerializeObject(charCreationObj.appearanceFaceFeatures);
            string appearanceDetails = JsonConvert.SerializeObject(charCreationObj.appearanceDetails);
            string appearanceHair = JsonConvert.SerializeObject(charCreationObj.appearanceHair);
            string appearanceClothes = JsonConvert.SerializeObject(charCreationObj.appearanceClothes);

            if (!string.IsNullOrEmpty(firstName) &&
                !string.IsNullOrEmpty(lastName) &&
                !string.IsNullOrEmpty(birthdayStr) &&
                !string.IsNullOrEmpty(genderIndex) &&
                !string.IsNullOrEmpty(appearanceParents) &&
                !string.IsNullOrEmpty(appearanceFaceFeatures) &&
                !string.IsNullOrEmpty(appearanceDetails) &&
                !string.IsNullOrEmpty(appearanceHair) &&
                !string.IsNullOrEmpty(appearanceClothes))
            {
                if (!DateTime.TryParseExact(birthdayStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime birthday))
                {
                    return;
                }

                var character = new Character
                {
                    AccountId = player.Account.SocialClubId,
                    FirstName = firstName,
                    LastName = lastName,
                    Birthday = birthday,
                    Model = genderIndex == "0" ? "mp_m_freemode_01" : "mp_f_freemode_01",
                    Armor = 0,
                    Health = 200,
                    Cash = 500,
                    Hunger = 100,
                    Thirst = 100,
                    AppearanceParents = appearanceParents,
                    AppearanceFaceFeatures = appearanceFaceFeatures,
                    AppearanceDetails = appearanceDetails,
                    AppearanceHair = appearanceHair,
                    AppearanceClothes = appearanceClothes
                };

                await _characterService.Create(character);

                player.Character = character;

                // necessary for intro
                await AltAsync.Do(() =>
                {
                    player.Model = Alt.Hash(player.Character.Model);
                });

                await Task.Delay(500);

                var appearanceData = new List<string> {
                    player.Character.AppearanceParents,
                    player.Character.AppearanceFaceFeatures,
                    player.Character.AppearanceDetails,
                    player.Character.AppearanceHair,
                    player.Character.AppearanceClothes
                };

                player.Emit("Session:PlayIntro", character.Id, appearanceData);

                return;
            }

            await player.KickAsync("Error on character creation!");
        }

        private void OnRequestTeleport(ServerPlayer player, Vector3 targetPosition)
        {
            if (player.IsInVehicle)
            {
                player.Vehicle.Position = targetPosition;
            }
            else
            {
                player.Position = targetPosition;
            }
        }

        private async Task SpawnCharacter(ServerPlayer player)
        {
            if (player.Character == null)
            {
                return;
            }

            await AltAsync.Do(() =>
            {
                player.Dimension = 0;
                player.Model = Alt.Hash(player.Character.Model);
                player.Position = SpawnPoint;
            });

            var appearanceData = new List<string> {
                player.Character.AppearanceParents,
                player.Character.AppearanceFaceFeatures,
                player.Character.AppearanceDetails,
                player.Character.AppearanceHair,
                player.Character.AppearanceClothes
            };

            player.Emit("Session:PlayerSpawning", appearanceData);
        }
    }
}
