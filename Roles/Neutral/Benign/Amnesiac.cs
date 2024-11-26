namespace MoreGamemodes
{
    public class Amnesiac : CustomRole
    {
        public override void OnHudUpate(HudManager __instance)
        {
            base.OnHudUpate(__instance);
            __instance.ReportButton.OverrideText("Remember");
        }

        public override bool OnReportDeadBody(NetworkedPlayerInfo target)
        {
            if (target == null) return true;
            ClassicGamemode.instance.ChangeRole(Player, target.GetRole().Role);
            if (target.Object == null) return false;
            if (target.GetRole().IsCrewmate())
                ClassicGamemode.instance.ChangeRole(target.Object, CustomRoles.Crewmate);
            else if (target.GetRole().IsImpostor())
                ClassicGamemode.instance.ChangeRole(target.Object, CustomRoles.Impostor);
            else if (target.GetRole().IsNeutral())
                ClassicGamemode.instance.ChangeRole(target.Object, CustomRoles.Opportunist);
            return false;
        }

        public override string GetNamePostfix()
        {
            if (SeeArrowToNearestBody.GetBool() && !Player.Data.IsDead && Player.GetClosestDeadBody() != null)
                return Utils.ColorString(Color, "\n" + Utils.GetArrow(Player.transform.position, Player.GetClosestDeadBody().transform.position));
            return "";
        }

        public Amnesiac(PlayerControl player)
        {
            Role = CustomRoles.Amnesiac;
            BaseRole = BaseRoles.Crewmate;
            Player = player;
            Utils.SetupRoleInfo(this);
            AbilityUses = -1f;
        }

        public static OptionItem Chance;
        public static OptionItem Count;
        public static OptionItem SeeArrowToNearestBody;
        public static void SetupOptionItem()
        {
            Chance = IntegerOptionItem.Create(800200, "Amnesiac", new(0, 100, 5), 0, TabGroup.NeutralRoles, false)
                .SetColor(CustomRolesHelper.RoleColors[CustomRoles.Amnesiac])
                .SetValueFormat(OptionFormat.Percent);
            Count = IntegerOptionItem.Create(800201, "Max", new(1, 15, 1), 1, TabGroup.NeutralRoles, false)
                .SetParent(Chance);
            SeeArrowToNearestBody = BooleanOptionItem.Create(800202, "See arrow to nearest body", false, TabGroup.CrewmateRoles, false)
                .SetParent(Chance);
            Options.RolesChance[CustomRoles.Amnesiac] = Chance;
            Options.RolesCount[CustomRoles.Amnesiac] = Count;
        }
    }
}