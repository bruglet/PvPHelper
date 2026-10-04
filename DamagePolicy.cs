namespace PvPHelper
{
    internal enum DamageActor { Other, Player, Drone }

    internal static class DamagePolicy
    {
        internal const ushort DefaultPvp = 50;
        internal const ushort DefaultDvp = 15;
        internal const int Step = 5;
        internal const int Maximum = 200;

        internal static bool IsValidPercent(int percent) => percent >= 0 && percent <= Maximum && percent % Step == 0;

        // Human control wins over catalog membership, including remote-operation bodies.
        internal static DamageActor Classify(bool playerControlled, bool catalogDrone) =>
            playerControlled ? DamageActor.Player : catalogDrone ? DamageActor.Drone : DamageActor.Other;

        internal static int GetPercent(DamageActor attacker, DamageActor victim, bool playerTeams,
            bool selfHit, bool delayedInstallment, int pvp, int dvp)
        {
            if (!playerTeams || selfHit || delayedInstallment || attacker == DamageActor.Other || victim == DamageActor.Other)
                return 100;
            return attacker == DamageActor.Drone && victim == DamageActor.Player ? dvp : pvp;
        }
    }
}
