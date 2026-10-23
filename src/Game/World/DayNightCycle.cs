namespace VoxelKing.Game.World;

using System;
using Godot;
using VoxelKing.Core.Common;
using VoxelKing.Core.Npc.Perception;

/// <summary>
/// Controls the in-game day/night cycle visuals, celestial rotation of the sun and moon,
/// directional light energy/color, procedural sky palette transitions, and ambient lighting.
/// </summary>
public partial class DayNightCycle : Node
{
    private static readonly Color DaySunColor = new(1.0f, 0.98f, 0.92f);
    private static readonly Color SunriseSunColor = new(1.0f, 0.70f, 0.40f);
    private static readonly Color SunsetSunColor = new(1.0f, 0.55f, 0.30f);
    private static readonly Color MoonLightColor = new(0.55f, 0.65f, 0.90f);

    private const float DaySunEnergy = 1.0f;
    private const float SunriseSunEnergy = 0.65f;
    private const float SunsetSunEnergy = 0.60f;
    private const float MoonLightEnergy = 0.20f;

    private static readonly Color DaySkyTop = new(0.384314f, 0.603922f, 0.854902f);
    private static readonly Color DaySkyHorizon = new(0.666667f, 0.772549f, 0.886275f);
    private static readonly Color DayGroundBottom = new(0.2f, 0.168627f, 0.133333f);
    private static readonly Color DayGroundHorizon = new(0.666667f, 0.772549f, 0.886275f);
    private static readonly Color DayAmbient = new(0.60f, 0.65f, 0.70f);
    private const float DayAmbientEnergy = 1.0f;

    private static readonly Color SunriseSkyTop = new(0.25f, 0.35f, 0.60f);
    private static readonly Color SunriseSkyHorizon = new(0.95f, 0.55f, 0.30f);
    private static readonly Color SunriseGroundBottom = new(0.15f, 0.12f, 0.10f);
    private static readonly Color SunriseGroundHorizon = new(0.85f, 0.45f, 0.25f);
    private static readonly Color SunriseAmbient = new(0.50f, 0.40f, 0.45f);
    private const float SunriseAmbientEnergy = 0.75f;

    private static readonly Color SunsetSkyTop = new(0.30f, 0.25f, 0.50f);
    private static readonly Color SunsetSkyHorizon = new(0.95f, 0.40f, 0.25f);
    private static readonly Color SunsetGroundBottom = new(0.15f, 0.10f, 0.10f);
    private static readonly Color SunsetGroundHorizon = new(0.80f, 0.35f, 0.20f);
    private static readonly Color SunsetAmbient = new(0.45f, 0.35f, 0.40f);
    private const float SunsetAmbientEnergy = 0.70f;

    private static readonly Color NightSkyTop = new(0.04f, 0.06f, 0.14f);
    private static readonly Color NightSkyHorizon = new(0.08f, 0.10f, 0.20f);
    private static readonly Color NightGroundBottom = new(0.05f, 0.05f, 0.08f);
    private static readonly Color NightGroundHorizon = new(0.08f, 0.10f, 0.20f);
    private static readonly Color NightAmbient = new(0.15f, 0.18f, 0.28f);
    private const float NightAmbientEnergy = 0.45f;

    private ProceduralSkyMaterial? _proceduralSky;
    private Godot.Environment? _environment;

    public IWorldTime? WorldTime { get; set; }
    public WorldConfig? Config { get; set; }
    public DirectionalLight3D? SunLight { get; set; }
    public WorldEnvironment? WorldEnvironment { get; set; }

    public bool IsTimeAdvancing { get; set; } = true;
    public float TimeScale { get; set; } = 1.0f;

    /// <summary>
    /// Initializes references to the simulation world clock, scene lighting, and environment sky.
    /// </summary>
    public void Initialize(
        IWorldTime worldTime,
        WorldConfig config,
        DirectionalLight3D? sunLight,
        WorldEnvironment? worldEnv)
    {
        WorldTime = worldTime ?? throw new ArgumentNullException(nameof(worldTime));
        Config = config;
        SunLight = sunLight;
        WorldEnvironment = worldEnv;

        if (WorldEnvironment?.Environment != null)
        {
            // Clone environment and sky material to prevent mutating source scene resources
            _environment = (Godot.Environment)WorldEnvironment.Environment.Duplicate();
            WorldEnvironment.Environment = _environment;

            if (_environment.Sky?.SkyMaterial is ProceduralSkyMaterial procSky)
            {
                _proceduralSky = (ProceduralSkyMaterial)procSky.Duplicate();
                _environment.Sky.SkyMaterial = _proceduralSky;
            }
        }

        UpdateVisuals();
    }

    /// <summary>
    /// Advances world clock simulation and updates lighting and sky visuals.
    /// </summary>
    public void Advance(float deltaSeconds)
    {
        if (!IsTimeAdvancing || WorldTime == null || deltaSeconds <= 0f)
        {
            return;
        }

        WorldTime.AdvanceSeconds(deltaSeconds * TimeScale);
        UpdateVisuals();
    }

    /// <summary>
    /// Updates directional light position, colors, and procedural sky material from the current world time.
    /// </summary>
    public void UpdateVisuals()
    {
        if (WorldTime == null)
        {
            return;
        }

        float hours = WorldTime.TimeOfDayHours;
        bool isDay = WorldTime.IsDay;

        // 1. Calculate Sun/Moon celestial rotation
        if (SunLight != null)
        {
            const float yaw = -30.0f;
            float pitch;

            if (isDay)
            {
                // Day half: 6.0h (0 deg) -> 12.0h (-90 deg, zenith) -> 18.0h (-180 deg)
                pitch = -((hours - 6.0f) / 12.0f) * 180.0f;
            }
            else
            {
                // Night half: 18.0h (0 deg) -> 0.0h (-90 deg, zenith moon) -> 6.0h (-180 deg)
                float nightHours = hours >= 18.0f ? hours - 18.0f : hours + 6.0f;
                pitch = -(nightHours / 12.0f) * 180.0f;
            }

            SunLight.RotationDegrees = new Vector3(pitch, yaw, 0f);
        }

        // 2. Sample interpolated lighting and sky palette
        SamplePalette(hours,
            out Color lightColor,
            out float lightEnergy,
            out Color skyTop,
            out Color skyHorizon,
            out Color groundBottom,
            out Color groundHorizon,
            out Color ambientColor,
            out float ambientEnergy);

        // 3. Apply light properties
        if (SunLight != null)
        {
            SunLight.LightColor = lightColor;
            SunLight.LightEnergy = lightEnergy;
        }

        // 4. Apply ambient environment
        if (_environment != null)
        {
            _environment.AmbientLightColor = ambientColor;
            _environment.AmbientLightEnergy = ambientEnergy;
        }

        // 5. Apply procedural sky material colors
        if (_proceduralSky != null)
        {
            _proceduralSky.SkyTopColor = skyTop;
            _proceduralSky.SkyHorizonColor = skyHorizon;
            _proceduralSky.GroundBottomColor = groundBottom;
            _proceduralSky.GroundHorizonColor = groundHorizon;
        }
    }

    private static void SamplePalette(
        float hours,
        out Color lightColor,
        out float lightEnergy,
        out Color skyTop,
        out Color skyHorizon,
        out Color groundBottom,
        out Color groundHorizon,
        out Color ambientColor,
        out float ambientEnergy)
    {
        // Segment 1: Deep Night [0.0 - 5.0)
        if (hours < 5.0f)
        {
            lightColor = MoonLightColor;
            lightEnergy = MoonLightEnergy;
            skyTop = NightSkyTop;
            skyHorizon = NightSkyHorizon;
            groundBottom = NightGroundBottom;
            groundHorizon = NightGroundHorizon;
            ambientColor = NightAmbient;
            ambientEnergy = NightAmbientEnergy;
        }
        // Segment 2: Dawn Transition (Night -> Sunrise) [5.0 - 6.0)
        else if (hours < 6.0f)
        {
            float t = SmoothStep(0f, 1f, (hours - 5.0f) / 1.0f);
            lightColor = MoonLightColor.Lerp(SunriseSunColor, t);
            lightEnergy = Mathf.Lerp(MoonLightEnergy, SunriseSunEnergy, t);
            skyTop = NightSkyTop.Lerp(SunriseSkyTop, t);
            skyHorizon = NightSkyHorizon.Lerp(SunriseSkyHorizon, t);
            groundBottom = NightGroundBottom.Lerp(SunriseGroundBottom, t);
            groundHorizon = NightGroundHorizon.Lerp(SunriseGroundHorizon, t);
            ambientColor = NightAmbient.Lerp(SunriseAmbient, t);
            ambientEnergy = Mathf.Lerp(NightAmbientEnergy, SunriseAmbientEnergy, t);
        }
        // Segment 3: Morning Transition (Sunrise -> Full Day) [6.0 - 7.5)
        else if (hours < 7.5f)
        {
            float t = SmoothStep(0f, 1f, (hours - 6.0f) / 1.5f);
            lightColor = SunriseSunColor.Lerp(DaySunColor, t);
            lightEnergy = Mathf.Lerp(SunriseSunEnergy, DaySunEnergy, t);
            skyTop = SunriseSkyTop.Lerp(DaySkyTop, t);
            skyHorizon = SunriseSkyHorizon.Lerp(DaySkyHorizon, t);
            groundBottom = SunriseGroundBottom.Lerp(DayGroundBottom, t);
            groundHorizon = SunriseGroundHorizon.Lerp(DayGroundHorizon, t);
            ambientColor = SunriseAmbient.Lerp(DayAmbient, t);
            ambientEnergy = Mathf.Lerp(SunriseAmbientEnergy, DayAmbientEnergy, t);
        }
        // Segment 4: Full Day [7.5 - 16.5)
        else if (hours < 16.5f)
        {
            lightColor = DaySunColor;
            lightEnergy = DaySunEnergy;
            skyTop = DaySkyTop;
            skyHorizon = DaySkyHorizon;
            groundBottom = DayGroundBottom;
            groundHorizon = DayGroundHorizon;
            ambientColor = DayAmbient;
            ambientEnergy = DayAmbientEnergy;
        }
        // Segment 5: Evening Transition (Full Day -> Sunset) [16.5 - 18.0)
        else if (hours < 18.0f)
        {
            float t = SmoothStep(0f, 1f, (hours - 16.5f) / 1.5f);
            lightColor = DaySunColor.Lerp(SunsetSunColor, t);
            lightEnergy = Mathf.Lerp(DaySunEnergy, SunsetSunEnergy, t);
            skyTop = DaySkyTop.Lerp(SunsetSkyTop, t);
            skyHorizon = DaySkyHorizon.Lerp(SunsetSkyHorizon, t);
            groundBottom = DayGroundBottom.Lerp(SunsetGroundBottom, t);
            groundHorizon = DayGroundHorizon.Lerp(SunsetGroundHorizon, t);
            ambientColor = DayAmbient.Lerp(SunsetAmbient, t);
            ambientEnergy = Mathf.Lerp(DayAmbientEnergy, SunsetAmbientEnergy, t);
        }
        // Segment 6: Dusk Transition (Sunset -> Night) [18.0 - 19.5)
        else if (hours < 19.5f)
        {
            float t = SmoothStep(0f, 1f, (hours - 18.0f) / 1.5f);
            lightColor = SunsetSunColor.Lerp(MoonLightColor, t);
            lightEnergy = Mathf.Lerp(SunsetSunEnergy, MoonLightEnergy, t);
            skyTop = SunsetSkyTop.Lerp(NightSkyTop, t);
            skyHorizon = SunsetSkyHorizon.Lerp(NightSkyHorizon, t);
            groundBottom = SunsetGroundBottom.Lerp(NightGroundBottom, t);
            groundHorizon = SunsetGroundHorizon.Lerp(NightGroundHorizon, t);
            ambientColor = SunsetAmbient.Lerp(NightAmbient, t);
            ambientEnergy = Mathf.Lerp(SunsetAmbientEnergy, NightAmbientEnergy, t);
        }
        // Segment 7: Deep Night [19.5 - 24.0]
        else
        {
            lightColor = MoonLightColor;
            lightEnergy = MoonLightEnergy;
            skyTop = NightSkyTop;
            skyHorizon = NightSkyHorizon;
            groundBottom = NightGroundBottom;
            groundHorizon = NightGroundHorizon;
            ambientColor = NightAmbient;
            ambientEnergy = NightAmbientEnergy;
        }
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }
}
