namespace PvPHelper
{
    internal enum DamageActor { Other, Player, Drone, EngineerTurret }
    internal enum DamageSetting { Pvp, Dvp, EngiTurret, MonsterScaling }

    internal static class DamagePolicy
    {
        internal const ushort DefaultPvp = 50;
        internal const ushort DefaultDvp = 15;
        internal const ushort DefaultEngiTurret = 25;
        internal const int Step = 5;
        internal const int Maximum = 200;

        internal static bool IsValidPercent(int percent) => percent >= 0 && percent <= Maximum && percent % Step == 0;

        internal static bool ShouldCap(DamageActor attacker, DamageActor victim, bool playerTeams, bool selfHit) =>
            playerTeams && !selfHit && attacker == DamageActor.Player && victim == DamageActor.Player;

        internal static float CapHit(float damage, float maximumHealth) =>
            System.Math.Min(damage, System.Math.Max(0f, maximumHealth) * (2f / 3f));

        // Human control wins over body category, including remote-operation bodies.
        internal static DamageActor Classify(bool playerControlled, bool catalogDrone, bool engineerTurret) =>
            playerControlled ? DamageActor.Player : engineerTurret ? DamageActor.EngineerTurret :
            catalogDrone ? DamageActor.Drone : DamageActor.Other;

        internal static int GetPercent(DamageActor attacker, DamageActor victim, bool playerTeams,
            bool selfHit, bool delayedInstallment, int pvp, int dvp, int engiTurret)
        {
            if (!playerTeams || selfHit || delayedInstallment || attacker == DamageActor.Other || victim == DamageActor.Other)
                return 100;
            if (attacker == DamageActor.EngineerTurret && victim == DamageActor.Player) return engiTurret;
            if (attacker == DamageActor.EngineerTurret && victim == DamageActor.EngineerTurret) return 100;
            return attacker == DamageActor.Drone && victim == DamageActor.Player ? dvp : pvp;
        }
    }
}
