using System.Collections.Generic;
using HarmonyLib;
using R2API;
using RoR2;
using RoR2.Networking;
using UnityEngine.Networking;

namespace PvPHelper
{
    internal static class PlayerTeams
    {
        internal static readonly string[] Names = { "Red", "Blue", "Green", "Yellow" };
        internal static readonly TeamIndex[] Teams = new TeamIndex[4];
        // Native message IDs below 32 are reserved. These IDs belong to PvPHelper.
        private const short RequestId = 18780;
        private const short StateId = 18781;
        private const byte Query = byte.MaxValue;
        private static readonly Dictionary<NetworkUserId, byte> serverChoices = new Dictionary<NetworkUserId, byte>();
        private static readonly Dictionary<NetworkInstanceId, byte> clientChoices = new Dictionary<NetworkInstanceId, byte>();

        internal static IEnumerable<TeamIndex> RewardTeams
        {
            get { yield return TeamIndex.Player; foreach (TeamIndex team in Teams) yield return team; }
        }

        internal static bool IsCustom(TeamIndex team) => System.Array.IndexOf(Teams, team) >= 0;
        internal static bool IsPlayerTeam(TeamIndex team) => team == TeamIndex.Player || IsCustom(team);

        internal static void Initialize()
        {
            TeamDef vanilla = TeamCatalog.GetTeamDef(TeamIndex.Player);
            for (int i = 0; i < Teams.Length; i++)
            {
                Teams[i] = TeamsAPI.RegisterTeam(new TeamDef
                {
                    nameToken = Names[i],
                    softCharacterLimit = vanilla.softCharacterLimit,
                    friendlyFireScaling = vanilla.friendlyFireScaling,
                    levelUpEffect = vanilla.levelUpEffect,
                    levelUpSound = vanilla.levelUpSound
                }, new TeamsAPI.TeamBehavior("PvPHelper_" + Names[i], TeamsAPI.TeamClassification.Player));
            }
            // R2API Core enforces matching versions for plugins with a hard R2API dependency.
            NetworkManagerSystem.onStartServerGlobal += StartServer;
            NetworkManagerSystem.onStartClientGlobal += StartClient;
            NetworkManagerSystem.onStopServerGlobal += Clear;
            NetworkManagerSystem.onStopClientGlobal += Clear;
            NetworkUser.onPostNetworkUserStart += UserStarted;
            MinionOwnership.onMinionOwnerChangedGlobal += OwnerChanged;
        }

        internal static void Shutdown()
        {
            NetworkManagerSystem.onStartServerGlobal -= StartServer;
            NetworkManagerSystem.onStartClientGlobal -= StartClient;
            NetworkManagerSystem.onStopServerGlobal -= Clear;
            NetworkManagerSystem.onStopClientGlobal -= Clear;
            NetworkUser.onPostNetworkUserStart -= UserStarted;
            MinionOwnership.onMinionOwnerChangedGlobal -= OwnerChanged;
            if (NetworkServer.active) NetworkServer.UnregisterHandler(RequestId);
            NetworkManagerSystem.singleton?.client?.UnregisterHandler(StateId);
            Clear();
        }

        private static void Clear() { serverChoices.Clear(); clientChoices.Clear(); DamageSettings.Reset(); }
        private static void StartServer() => NetworkServer.RegisterHandler(RequestId, ReceiveRequest);
        private static void StartClient(NetworkClient client) => client.RegisterHandler(StateId, ReceiveState);

        private static void UserStarted(NetworkUser user)
        {
            if (NetworkServer.active)
            {
                if (!serverChoices.ContainsKey(user.id)) serverChoices[user.id] = 0;
                if (user.master) Apply(user.master);
                Broadcast();
            }
            if (user.isLocalPlayer && !NetworkServer.active) Send(user, Query);
        }

        internal static byte GetChoice(NetworkUser user)
        {
            byte choice;
            return NetworkServer.active
                ? (serverChoices.TryGetValue(user.id, out choice) ? choice : (byte)0)
                : (clientChoices.TryGetValue(user.netId, out choice) ? choice : (byte)0);
        }

        internal static void Select(NetworkUser user, byte choice)
        {
            if (!user.isLocalPlayer || choice >= Teams.Length) return;
            if (NetworkServer.active) SetChoice(user, choice);
            else Send(user, choice);
        }

        private static void Send(NetworkUser user, byte choice)
        {
            NetworkManagerSystem.singleton.client?.Send(RequestId, new ChoiceMessage { User = user.netId, Choice = choice });
        }

        private static void ReceiveRequest(NetworkMessage message)
        {
            ChoiceMessage request = message.ReadMessage<ChoiceMessage>();
            var obj = NetworkServer.FindLocalObject(request.User);
            NetworkUser? user = obj ? obj.GetComponent<NetworkUser>() : null;
            // A client may only change its own local users, never another client's player.
            if (user is null || !user || user.connectionToClient != message.conn) return;
            if (request.Choice != Query) SetChoice(user, request.Choice);
            else Broadcast();
        }

        private static void SetChoice(NetworkUser user, byte choice)
        {
            if (choice >= Teams.Length || Run.instance || !PreGameController.instance) return;
            serverChoices[user.id] = choice;
            if (user.master) Apply(user.master);
            Broadcast();
        }

        internal static void Broadcast()
        {
            var state = new StateMessage
            {
                PvpPercent = DamageSettings.PvpPercent, DvpPercent = DamageSettings.DvpPercent,
                EngiTurretPercent = DamageSettings.EngiTurretPercent
            };
            foreach (NetworkUser user in NetworkUser.readOnlyInstancesList)
                state.Choices.Add(new ChoiceMessage { User = user.netId, Choice = GetChoice(user) });
            NetworkServer.SendToAll(StateId, state);
        }

        private static void ReceiveState(NetworkMessage message)
        {
            StateMessage state = message.ReadMessage<StateMessage>();
            DamageSettings.Receive(state.PvpPercent, state.DvpPercent, state.EngiTurretPercent);
            clientChoices.Clear();
            foreach (ChoiceMessage choice in state.Choices)
                if (choice.Choice < Teams.Length) clientChoices[choice.User] = choice.Choice;
        }

        private static void Apply(CharacterMaster master)
        {
            if (!NetworkServer.active) return;
            NetworkUser? user = master.playerCharacterMasterController?.networkUser;
            if (user is not null && user) master.teamIndex = Teams[GetChoice(user)];
            else if (master.minionOwnership && master.minionOwnership.ownerMaster &&
                IsCustom(master.minionOwnership.ownerMaster.teamIndex))
                master.teamIndex = master.minionOwnership.ownerMaster.teamIndex;
        }

        private static void OwnerChanged(MinionOwnership ownership)
        {
            if (!NetworkServer.active || !ownership.ownerMaster || !IsCustom(ownership.ownerMaster.teamIndex)) return;
            CharacterMaster master = ownership.GetComponent<CharacterMaster>();
            if (!master) return;
            master.teamIndex = ownership.ownerMaster.teamIndex;
            CharacterBody body = master.GetBody();
            if (body && body.teamComponent) body.teamComponent.teamIndex = master.teamIndex;
        }

        [HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.SpawnBody))]
        private static class AssignBeforeBodySpawn
        {
            private static void Prefix(CharacterMaster __instance) => Apply(__instance);
        }

        public sealed class ChoiceMessage : MessageBase
        {
            public NetworkInstanceId User;
            public byte Choice;
            public override void Serialize(NetworkWriter writer) { writer.Write(User); writer.Write(Choice); }
            public override void Deserialize(NetworkReader reader) { User = reader.ReadNetworkId(); Choice = reader.ReadByte(); }
        }

        public sealed class StateMessage : MessageBase
        {
            public ushort PvpPercent = DamagePolicy.DefaultPvp;
            public ushort DvpPercent = DamagePolicy.DefaultDvp;
            public ushort EngiTurretPercent = DamagePolicy.DefaultEngiTurret;
            public readonly List<ChoiceMessage> Choices = new List<ChoiceMessage>();
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(PvpPercent);
                writer.Write(DvpPercent);
                writer.Write(EngiTurretPercent);
                writer.Write((ushort)Choices.Count);
                foreach (ChoiceMessage choice in Choices) choice.Serialize(writer);
            }
            public override void Deserialize(NetworkReader reader)
            {
                PvpPercent = reader.ReadUInt16();
                DvpPercent = reader.ReadUInt16();
                EngiTurretPercent = reader.ReadUInt16();
                Choices.Clear();
                int count = reader.ReadUInt16();
                for (int i = 0; i < count; i++) { var choice = new ChoiceMessage(); choice.Deserialize(reader); Choices.Add(choice); }
            }
        }
    }
}
