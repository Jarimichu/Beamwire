using Beamwire;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace JarimichusLasers
{
    /// <summary>
    /// Jarimichus Lasers: Beamwire (wireless power by laser), the mining laser, the laser cannon and the laser
    /// control console. One settings window covers every part.
    /// </summary>
    public class LasersMod : Mod
    {
        public static LasersSettings Settings;
        private Vector2 scroll;
        private float viewHeight = 1200f;

        public LasersMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LasersSettings>();
            new Harmony("jarimichu.lasers").PatchAll();
        }

        public override string SettingsCategory() => "Jarimichus Lasers";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect view = new Rect(0f, 0f, inRect.width - 20f, viewHeight);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            var l = new Listing_Standard();
            l.Begin(view);
            LasersSettings s = Settings;

            // ---- parts
            Header(l, "JLS_Header_Parts");
            l.Label("JLS_Parts_Desc".Translate());
            bool bw = s.beamwireEnabled, ml = s.miningLaserEnabled, lc = s.laserCannonEnabled, lg = s.laserGateEnabled;
            l.CheckboxLabeled("JLS_Part_Beamwire".Translate(), ref s.beamwireEnabled, "JLS_Part_Beamwire_Desc".Translate());
            l.CheckboxLabeled("JLS_Part_MiningLaser".Translate(), ref s.miningLaserEnabled, "JLS_Part_MiningLaser_Desc".Translate());
            l.CheckboxLabeled("JLS_Part_LaserCannon".Translate(), ref s.laserCannonEnabled, "JLS_Part_LaserCannon_Desc".Translate());
            l.CheckboxLabeled("JLS_Part_LaserGate".Translate(), ref s.laserGateEnabled, "JLS_Part_LaserGate_Desc".Translate());
            if (bw != s.beamwireEnabled || ml != s.miningLaserEnabled || lc != s.laserCannonEnabled || lg != s.laserGateEnabled) s.partsChanged = true;
            if (s.partsChanged)
            {
                GUI.color = ColorLibrary.RedReadable;
                l.Label("JLS_Parts_Restart".Translate());
                GUI.color = Color.white;
            }

            // ---- Beamwire
            if (s.beamwireEnabled)
            {
                Header(l, "JLS_Header_Beamwire");
                BeamwireSettings b = s.beamwire;
                l.CheckboxLabeled("Beamwire_Setting_AutoAim".Translate(), ref b.autoAim, "Beamwire_Setting_AutoAim_Desc".Translate());
                l.CheckboxLabeled("Beamwire_Setting_Damage".Translate(), ref b.beamDamage, "Beamwire_Setting_Damage_Desc".Translate());
                if (b.beamDamage)
                {
                    l.Label("Beamwire_Setting_DamageMult".Translate(b.damageMultiplier.ToStringPercent()));
                    b.damageMultiplier = l.Slider(b.damageMultiplier, 0.1f, 3f);
                }
                l.CheckboxLabeled("Beamwire_Setting_ShowBeams".Translate(), ref b.showBeams, "Beamwire_Setting_ShowBeams_Desc".Translate());
                l.CheckboxLabeled("Beamwire_Setting_Weather".Translate(), ref b.weatherAffects, "Beamwire_Setting_Weather_Desc".Translate());
                l.CheckboxLabeled("Beamwire_Setting_RoofBlocks".Translate(), ref b.roofsBlock, "Beamwire_Setting_RoofBlocks_Desc".Translate());
                l.CheckboxLabeled("Beamwire_Setting_Dust".Translate(), ref b.dustEnabled, "Beamwire_Setting_Dust_Desc".Translate());
                l.Label("Beamwire_Setting_NodeLoss".Translate(b.nodeLossPercent.ToString("0.#")));
                b.nodeLossPercent = Mathf.Round(l.Slider(b.nodeLossPercent, 0f, 25f) * 2f) / 2f;
                l.Label("Beamwire_Setting_DistanceLoss".Translate(b.lossPerTenTiles.ToString("0.#")));
                b.lossPerTenTiles = Mathf.Round(l.Slider(b.lossPerTenTiles, 0f, 10f) * 2f) / 2f;
                l.Label("Beamwire_Setting_Range".Translate(b.maxRange));
                b.maxRange = Mathf.RoundToInt(l.Slider(b.maxRange, 10f, 200f));
            }

            // ---- mining laser
            if (s.miningLaserEnabled)
            {
                Header(l, "JLS_Header_MiningLaser");
                l.CheckboxLabeled("JML_Setting_Burn".Translate(), ref s.miningBurn, "JML_Setting_Burn_Desc".Translate());
                if (s.miningBurn)
                {
                    l.Label("JML_Setting_BurnMult".Translate(s.miningBurnMultiplier.ToStringPercent()));
                    s.miningBurnMultiplier = l.Slider(s.miningBurnMultiplier, 0.1f, 3f);
                }
            }

            // ---- laser cannon
            if (s.laserCannonEnabled)
            {
                Header(l, "JLS_Header_LaserCannon");
                l.Label("JLC_Setting_DamageMult".Translate(s.cannonDamageMultiplier.ToStringPercent()));
                s.cannonDamageMultiplier = l.Slider(s.cannonDamageMultiplier, 0.1f, 3f);
                l.CheckboxLabeled("JLC_Setting_Animals".Translate(), ref s.cannonAttackAnimals, "JLC_Setting_Animals_Desc".Translate());
            }

            l.Gap();
            if (l.ButtonText("JLS_Reset".Translate()))
            {
                s.ResetTuning();
            }
            viewHeight = l.CurHeight + 20f;
            l.End();
            Widgets.EndScrollView();
        }

        private static void Header(Listing_Standard l, string key)
        {
            l.Gap(14f);
            Text.Font = GameFont.Medium;
            l.Label(key.Translate());
            Text.Font = GameFont.Small;
            l.GapLine(6f);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            if (Current.ProgramState == ProgramState.Playing) MapComponent_BeamNetwork.DirtyAllMaps();
        }
    }

    public class LasersSettings : ModSettings
    {
        // Parts: read while the game loads (by the part toggle patches), so changes need a restart.
        public bool beamwireEnabled = true;
        public bool miningLaserEnabled = true;
        public bool laserCannonEnabled = true;
        public bool laserGateEnabled = true;

        /// <summary>Set when a part toggle changed this session, to show the restart notice.</summary>
        public bool partsChanged;

        public BeamwireSettings beamwire = new BeamwireSettings();

        public bool miningBurn = true;
        public float miningBurnMultiplier = 1f;

        public float cannonDamageMultiplier = 1f;
        public bool cannonAttackAnimals = true;

        public bool PartEnabled(string part)
        {
            switch (part)
            {
                case "Beamwire": return beamwireEnabled;
                case "MiningLaser": return miningLaserEnabled;
                case "LaserCannon": return laserCannonEnabled;
                case "LaserGate": return laserGateEnabled;
                default:
                    Log.Error($"[Jarimichus Lasers] Unknown part '{part}' in a patch.");
                    return true;
            }
        }

        /// <summary>Resets every tuning setting to its default. Leaves the parts alone.</summary>
        public void ResetTuning()
        {
            beamwire.Reset();
            miningBurn = true;
            miningBurnMultiplier = 1f;
            cannonDamageMultiplier = 1f;
            cannonAttackAnimals = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref beamwireEnabled, "beamwireEnabled", true);
            Scribe_Values.Look(ref miningLaserEnabled, "miningLaserEnabled", true);
            Scribe_Values.Look(ref laserCannonEnabled, "laserCannonEnabled", true);
            Scribe_Values.Look(ref laserGateEnabled, "laserGateEnabled", true);
            Scribe_Deep.Look(ref beamwire, "beamwire");
            Scribe_Values.Look(ref miningBurn, "miningBurn", true);
            Scribe_Values.Look(ref miningBurnMultiplier, "miningBurnMultiplier", 1f);
            Scribe_Values.Look(ref cannonDamageMultiplier, "cannonDamageMultiplier", 1f);
            Scribe_Values.Look(ref cannonAttackAnimals, "cannonAttackAnimals", true);
            if (beamwire == null) beamwire = new BeamwireSettings();
        }
    }

    /// <summary>
    /// Runs its operation only when the listed parts of the mod are switched on (<c>enabled</c>) and/or off
    /// (<c>disabled</c>) in the settings. Lets players remove whole parts, and keeps patches that touch two
    /// parts (e.g. beam power for the lasers) from running when either part is gone.
    /// <code>
    /// &lt;Operation Class="JarimichusLasers.PatchOperationIfParts"&gt;
    ///   &lt;disabled&gt;&lt;li&gt;MiningLaser&lt;/li&gt;&lt;/disabled&gt;
    ///   &lt;operation Class="PatchOperationRemove"&gt;...&lt;/operation&gt;
    /// &lt;/Operation&gt;
    /// </code>
    /// </summary>
    public class PatchOperationIfParts : PatchOperation
    {
        public System.Collections.Generic.List<string> enabled;
        public System.Collections.Generic.List<string> disabled;
        public PatchOperation operation;

        protected override bool ApplyWorker(System.Xml.XmlDocument xml)
        {
            LasersSettings s = LasersMod.Settings;
            if (s == null) return true;
            if (enabled != null && !enabled.TrueForAll(s.PartEnabled)) return true;
            if (disabled != null && !disabled.TrueForAll(p => !s.PartEnabled(p))) return true;
            return operation == null || operation.Apply(xml);
        }
    }

    /// <summary>
    /// The mod's own defs, looked up by name on first use rather than through [DefOf], so a part switched off
    /// in the settings (and so missing from the game) never causes "failed to find" errors.
    /// </summary>
    public static class LasersDefOf
    {
        private static DamageDef miningBurn, cannonBurn;
        private static JobDef manCannon, manConsole;
        private static SoundDef buzz;

        public static DamageDef JML_LaserBurn => miningBurn ?? (miningBurn = DefDatabase<DamageDef>.GetNamedSilentFail("JML_LaserBurn"));
        public static DamageDef JLC_LaserBurn => cannonBurn ?? (cannonBurn = DefDatabase<DamageDef>.GetNamedSilentFail("JLC_LaserBurn"));
        private static ThingDef curtain;
        private static DamageDef gateBurn;
        public static ThingDef JLG_LaserCurtain => curtain ?? (curtain = DefDatabase<ThingDef>.GetNamedSilentFail("JLG_LaserCurtain"));
        public static DamageDef JLG_GateBurn => gateBurn ?? (gateBurn = DefDatabase<DamageDef>.GetNamedSilentFail("JLG_GateBurn"));
        public static JobDef JLC_ManLaserCannon => manCannon ?? (manCannon = DefDatabase<JobDef>.GetNamedSilentFail("JLC_ManLaserCannon"));
        public static JobDef JLX_ManLaserConsole => manConsole ?? (manConsole = DefDatabase<JobDef>.GetNamedSilentFail("JLX_ManLaserConsole"));
        public static SoundDef JLC_LaserBuzz => buzz ?? (buzz = DefDatabase<SoundDef>.GetNamedSilentFail("JLC_LaserBuzz"));
    }
}
