using RoR2;
using UnityEngine.Networking;

namespace PvPHelper
{
    internal static class DamageSettings
    {
        internal static ushort PvpPercent { get; private set; } = DamagePolicy.DefaultPvp;
        internal static ushort DvpPercent { get; private set; } = DamagePolicy.DefaultDvp;
        internal static ushort EngiTurretPercent { get; private set; } = DamagePolicy.DefaultEngiTurret;

        internal static bool CanEdit => NetworkServer.active && !Run.instance && PreGameController.instance &&
            PreGameController.instance.IsCharacterSwitchingCurrentlyAllowed();

        internal static ushort Get(DamageSetting setting) => setting switch
        {
            DamageSetting.Pvp => PvpPercent,
            DamageSetting.Dvp => DvpPercent,
            DamageSetting.EngiTurret => EngiTurretPercent,
            _ => throw new System.ArgumentOutOfRangeException(nameof(setting))
        };

        internal static void Set(DamageSetting setting, int percent)
        {
            if (!CanEdit || !DamagePolicy.IsValidPercent(percent)) return;
            switch (setting)
            {
                case DamageSetting.Pvp: PvpPercent = (ushort)percent; break;
                case DamageSetting.Dvp: DvpPercent = (ushort)percent; break;
                case DamageSetting.EngiTurret: EngiTurretPercent = (ushort)percent; break;
                default: return;
            }
            PlayerTeams.Broadcast();
        }

        internal static void Receive(ushort pvp, ushort dvp, ushort engiTurret)
        {
            if (NetworkServer.active || !DamagePolicy.IsValidPercent(pvp) || !DamagePolicy.IsValidPercent(dvp) ||
                !DamagePolicy.IsValidPercent(engiTurret)) return;
            PvpPercent = pvp;
            DvpPercent = dvp;
            EngiTurretPercent = engiTurret;
        }

        internal static void Reset()
        {
            PvpPercent = DamagePolicy.DefaultPvp;
            DvpPercent = DamagePolicy.DefaultDvp;
            EngiTurretPercent = DamagePolicy.DefaultEngiTurret;
        }
    }
}
