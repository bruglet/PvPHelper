using RoR2;
using UnityEngine.Networking;

namespace PvPHelper
{
    internal static class DamageSettings
    {
        internal static ushort PvpPercent { get; private set; } = DamagePolicy.DefaultPvp;
        internal static ushort DvpPercent { get; private set; } = DamagePolicy.DefaultDvp;

        internal static bool CanEdit => NetworkServer.active && !Run.instance && PreGameController.instance &&
            PreGameController.instance.IsCharacterSwitchingCurrentlyAllowed();

        internal static void Set(bool droneVsPlayer, int percent)
        {
            if (!CanEdit || !DamagePolicy.IsValidPercent(percent)) return;
            if (droneVsPlayer) DvpPercent = (ushort)percent;
            else PvpPercent = (ushort)percent;
            PlayerTeams.Broadcast();
        }

        internal static void Receive(ushort pvp, ushort dvp)
        {
            if (NetworkServer.active || !DamagePolicy.IsValidPercent(pvp) || !DamagePolicy.IsValidPercent(dvp)) return;
            PvpPercent = pvp;
            DvpPercent = dvp;
        }

        internal static void Reset()
        {
            PvpPercent = DamagePolicy.DefaultPvp;
            DvpPercent = DamagePolicy.DefaultDvp;
        }
    }
}
