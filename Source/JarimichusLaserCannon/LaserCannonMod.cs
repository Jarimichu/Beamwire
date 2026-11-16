using UnityEngine;
using Verse;

namespace JarimichusLaserCannon
{
    public class LaserCannonMod : Mod
    {
        public static LaserCannonSettings Settings;

        public LaserCannonMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LaserCannonSettings>();
        }

        public override string SettingsCategory() => "Jarimichus Laser Cannon";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var l = new Listing_Standard();
            l.Begin(inRect);
            l.Label("JLC_Setting_ArtStyle".Translate());
            foreach (string style in LaserStyles.Folders)
            {
                if (l.RadioButton(("JLC_Style_" + style).Translate(), Settings.artStyle == style, 12f))
                {
                    Settings.artStyle = style;
                    LaserStyles.Apply();
                }
            }
            if (LaserStyles.BeamwireInstalled)
            {
                bool before = Settings.followBeamwire;
                l.CheckboxLabeled("JLC_Setting_FollowBeamwire".Translate(), ref Settings.followBeamwire, "JLC_Setting_FollowBeamwire_Desc".Translate());
                if (before != Settings.followBeamwire) LaserStyles.Apply();
            }
            l.GapLine();
            l.Label("JLC_Setting_DamageMult".Translate(Settings.damageMultiplier.ToStringPercent()));
            Settings.damageMultiplier = l.Slider(Settings.damageMultiplier, 0.1f, 3f);
            l.CheckboxLabeled("JLC_Setting_Animals".Translate(), ref Settings.attackAnimals, "JLC_Setting_Animals_Desc".Translate());
            l.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            LaserStyles.Apply();
        }
    }

    public class LaserCannonSettings : ModSettings
    {
        public string artStyle = "Standard";
        public bool followBeamwire = true;
        public float damageMultiplier = 1f;
        public bool attackAnimals = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref artStyle, "artStyle", "Standard");
            Scribe_Values.Look(ref followBeamwire, "followBeamwire", true);
            Scribe_Values.Look(ref damageMultiplier, "damageMultiplier", 1f);
            Scribe_Values.Look(ref attackAnimals, "attackAnimals", true);
        }
    }

    [RimWorld.DefOf]
    public static class LaserCannonDefOf
    {
        public static DamageDef JLC_LaserBurn;
        public static JobDef JLC_ManLaserCannon;
        public static SoundDef JLC_LaserBuzz;

        static LaserCannonDefOf()
        {
            RimWorld.DefOfHelper.EnsureInitializedInCtor(typeof(LaserCannonDefOf));
        }
    }
}
