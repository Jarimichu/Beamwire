using UnityEngine;
using Verse;

namespace JarimichusMiningLaser
{
    public class MiningLaserMod : Mod
    {
        public static MiningLaserSettings Settings;

        public MiningLaserMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MiningLaserSettings>();
        }

        public override string SettingsCategory() => "Jarimichus Mining Laser";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var l = new Listing_Standard();
            l.Begin(inRect);
            l.Label("JML_Setting_ArtStyle".Translate());
            foreach (string style in LaserStyles.Folders)
            {
                if (l.RadioButton(("JML_Style_" + style).Translate(), Settings.artStyle == style, 12f))
                {
                    Settings.artStyle = style;
                    LaserStyles.Apply();
                }
            }
            if (LaserStyles.BeamwireInstalled)
            {
                bool before = Settings.followBeamwire;
                l.CheckboxLabeled("JML_Setting_FollowBeamwire".Translate(), ref Settings.followBeamwire, "JML_Setting_FollowBeamwire_Desc".Translate());
                if (before != Settings.followBeamwire) LaserStyles.Apply();
            }
            l.GapLine();
            l.CheckboxLabeled("JML_Setting_Burn".Translate(), ref Settings.burn, "JML_Setting_Burn_Desc".Translate());
            if (Settings.burn)
            {
                l.Label("JML_Setting_BurnMult".Translate(Settings.burnMultiplier.ToStringPercent()));
                Settings.burnMultiplier = l.Slider(Settings.burnMultiplier, 0.1f, 3f);
            }
            l.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            LaserStyles.Apply();
        }
    }

    public class MiningLaserSettings : ModSettings
    {
        public string artStyle = "Standard";
        public bool followBeamwire = true;
        public bool burn = true;
        public float burnMultiplier = 1f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref artStyle, "artStyle", "Standard");
            Scribe_Values.Look(ref followBeamwire, "followBeamwire", true);
            Scribe_Values.Look(ref burn, "burn", true);
            Scribe_Values.Look(ref burnMultiplier, "burnMultiplier", 1f);
        }
    }

    [RimWorld.DefOf]
    public static class MiningLaserDefOf
    {
        public static DamageDef JML_LaserBurn;

        static MiningLaserDefOf()
        {
            RimWorld.DefOfHelper.EnsureInitializedInCtor(typeof(MiningLaserDefOf));
        }
    }
}
