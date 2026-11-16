using Beamwire.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace Beamwire
{
    /// <summary>Beamwire's settings live in the Jarimichus Lasers settings (see <see cref="JarimichusLasers.LasersMod"/>).</summary>
    public static class BeamwireMod
    {
        public static BeamwireSettings Settings => JarimichusLasers.LasersMod.Settings?.beamwire;
    }

    /// <summary>Beamwire's part of the mod settings. Saved inside <see cref="JarimichusLasers.LasersSettings"/>.</summary>
    public class BeamwireSettings : IExposable
    {
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

        public void ExposeData()
        {
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

    /// <summary>Beamwire's defs, looked up by name on first use (the Beamwire part can be switched off).</summary>
    public static class BeamwireDefOf
    {
        private static DamageDef burn;
        private static JobDef clean;
        private static SoundDef hum;

        public static DamageDef Beamwire_BeamBurn => burn ?? (burn = DefDatabase<DamageDef>.GetNamedSilentFail("Beamwire_BeamBurn"));
        public static JobDef Beamwire_CleanOptics => clean ?? (clean = DefDatabase<JobDef>.GetNamedSilentFail("Beamwire_CleanOptics"));
        public static SoundDef Beamwire_EmitterHum => hum ?? (hum = DefDatabase<SoundDef>.GetNamedSilentFail("Beamwire_EmitterHum"));
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
