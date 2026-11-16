using Beamwire.Core;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Beamwire
{
    public class BeamwireMod : Mod
    {
        public static BeamwireSettings Settings;

        public BeamwireMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BeamwireSettings>();
            new Harmony("jarimichu.beamwire").PatchAll();
        }

        public override string SettingsCategory() => "Beamwire";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var l = new Listing_Standard();
            l.Begin(inRect);

            l.CheckboxLabeled("Beamwire_Setting_AutoAim".Translate(), ref Settings.autoAim, "Beamwire_Setting_AutoAim_Desc".Translate());
            l.GapLine();

            l.CheckboxLabeled("Beamwire_Setting_Damage".Translate(), ref Settings.beamDamage, "Beamwire_Setting_Damage_Desc".Translate());
            if (Settings.beamDamage)
            {
                l.Label("Beamwire_Setting_DamageMult".Translate(Settings.damageMultiplier.ToStringPercent()));
                Settings.damageMultiplier = l.Slider(Settings.damageMultiplier, 0.1f, 3f);
            }
            l.GapLine();

            l.Label("Beamwire_Setting_ArtStyle".Translate());
            foreach (BeamwireStyle style in BeamwireStyles.All)
            {
                if (l.RadioButton(style.Label, Settings.artStyle == style.Folder, 12f))
                {
                    Settings.artStyle = style.Folder;
                    BeamwireStyles.Apply();
                }
            }
            l.GapLine();

            l.CheckboxLabeled("Beamwire_Setting_ShowBeams".Translate(), ref Settings.showBeams, "Beamwire_Setting_ShowBeams_Desc".Translate());
            l.CheckboxLabeled("Beamwire_Setting_Weather".Translate(), ref Settings.weatherAffects, "Beamwire_Setting_Weather_Desc".Translate());
            l.CheckboxLabeled("Beamwire_Setting_RoofBlocks".Translate(), ref Settings.roofsBlock, "Beamwire_Setting_RoofBlocks_Desc".Translate());
            l.CheckboxLabeled("Beamwire_Setting_Dust".Translate(), ref Settings.dustEnabled, "Beamwire_Setting_Dust_Desc".Translate());
            l.GapLine();

            l.Label("Beamwire_Setting_NodeLoss".Translate(Settings.nodeLossPercent.ToString("0.#")));
            Settings.nodeLossPercent = Mathf.Round(l.Slider(Settings.nodeLossPercent, 0f, 25f) * 2f) / 2f;
            l.Label("Beamwire_Setting_DistanceLoss".Translate(Settings.lossPerTenTiles.ToString("0.#")));
            Settings.lossPerTenTiles = Mathf.Round(l.Slider(Settings.lossPerTenTiles, 0f, 10f) * 2f) / 2f;
            l.Label("Beamwire_Setting_Range".Translate(Settings.maxRange));
            Settings.maxRange = Mathf.RoundToInt(l.Slider(Settings.maxRange, 10f, 200f));

            l.Gap();
            if (l.ButtonText("Beamwire_Setting_Reset".Translate()))
            {
                Settings.Reset();
                BeamwireStyles.Apply();
            }

            l.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            MapComponent_BeamNetwork.DirtyAllMaps();
        }
    }

    public class BeamwireSettings : ModSettings
    {
        public string artStyle = "Standard";
        public bool autoAim = true;
        public bool beamDamage = true;
        public float damageMultiplier = 1f;
        public bool showBeams = true;
        public bool weatherAffects = true;
        public bool roofsBlock;
        public bool dustEnabled = true;
        public float nodeLossPercent = 5f;
        public float lossPerTenTiles = 1f;
        public int maxRange = 60;

        public void Reset()
        {
            artStyle = "Standard";
            autoAim = true;
            beamDamage = true;
            damageMultiplier = 1f;
            showBeams = true;
            weatherAffects = true;
            roofsBlock = false;
            dustEnabled = true;
            nodeLossPercent = 5f;
            lossPerTenTiles = 1f;
            maxRange = 60;
        }

        public BeamSettings ToBeamSettings() => new BeamSettings
        {
            NodeLoss = nodeLossPercent / 100f,
            LossPerTile = lossPerTenTiles / 1000f,
            MaxRange = maxRange,
        };

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref artStyle, "artStyle", "Standard");
            Scribe_Values.Look(ref autoAim, "autoAim", true);
            Scribe_Values.Look(ref beamDamage, "beamDamage", true);
            Scribe_Values.Look(ref damageMultiplier, "damageMultiplier", 1f);
            Scribe_Values.Look(ref showBeams, "showBeams", true);
            Scribe_Values.Look(ref weatherAffects, "weatherAffects", true);
            Scribe_Values.Look(ref roofsBlock, "roofsBlock", false);
            Scribe_Values.Look(ref dustEnabled, "dustEnabled", true);
            Scribe_Values.Look(ref nodeLossPercent, "nodeLossPercent", 5f);
            Scribe_Values.Look(ref lossPerTenTiles, "lossPerTenTiles", 1f);
            Scribe_Values.Look(ref maxRange, "maxRange", 60);
        }
    }

    [DefOf]
    public static class BeamwireDefOf
    {
        public static DamageDef Beamwire_BeamBurn;
        public static JobDef Beamwire_CleanOptics;
        public static SoundDef Beamwire_EmitterHum;

        static BeamwireDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BeamwireDefOf));
        }
    }

    [StaticConstructorOnStartup]
    public static class BeamwireTex
    {
        public static readonly Material BeamCore = MaterialPool.MatFrom("Beamwire/Effects/BeamCore", ShaderDatabase.Transparent);
        public static readonly Material BeamGlow = MaterialPool.MatFrom("Beamwire/Effects/BeamGlow", ShaderDatabase.MoteGlow);
        public static readonly Texture2D ShowBeams = ContentFinder<Texture2D>.Get("Beamwire/UI/ShowBeams");
        public static readonly Texture2D Throughput = ContentFinder<Texture2D>.Get("Beamwire/UI/Throughput");
        public static readonly Texture2D AutoAim = ContentFinder<Texture2D>.Get("Beamwire/UI/AutoAim");
        public static readonly Texture2D ShutterOpen = ContentFinder<Texture2D>.Get("Beamwire/UI/ShutterOpen");
        public static readonly Texture2D ShutterClosed = ContentFinder<Texture2D>.Get("Beamwire/UI/ShutterClosed");
    }
}
