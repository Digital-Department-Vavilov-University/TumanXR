using UnityEngine;

[ExecuteAlways]
public class DualMistControllerSimple : MonoBehaviour
{
    [System.Serializable]
    public class SharedSettings
    {
        [Header("Shared Volume")]
        public Vector3 boxSize = new Vector3(14f, 2f, 1.5f);
        public Material particleMaterial;

        [Header("General")]
        public bool playOnStart = true;
        public bool alwaysSimulateOffscreen = true;
    }

    [System.Serializable]
    public class LocalLayerSettings
    {
        [Header("Local Layer")]
        public bool enabled = true;
        public int particlesPerSecond = 180;
        public int maxParticles = 1500;
        public Vector2 lifetimeRange = new Vector2(2f, 3.5f);
        public Vector2 startSizeRange = new Vector2(0.8f, 1.6f);
        [Range(0f, 1f)] public float peakAlpha = 0.12f;
    }

    [System.Serializable]
    public class TrailLayerSettings
    {
        [Header("World Trail Layer")]
        public bool enabled = true;
        public int particlesPerSecond = 180;
        public int maxParticles = 1500;
        public Vector2 lifetimeRange = new Vector2(0.85f, 1.25f);
        public Vector2 startSizeRange = new Vector2(0.8f, 1.3f);
        [Range(0f, 1f)] public float peakAlpha = 0.12f;
        public float downwardVelocity = 0f;

        [Header("Motion Control")]
        [Tooltip("Минимальная скорость объекта, после которой хвост начинает включаться")]
        public float minSpeedToEmit = 0.03f;

        [Tooltip("Насколько плавно сглаживается измеренная скорость")]
        public float speedSmoothing = 8f;

        [Tooltip("Скорость плавного включения/выключения хвоста")]
        public float emissionBlendSpeed = 3.5f;
    }

    public SharedSettings shared = new SharedSettings();
    public LocalLayerSettings localLayer = new LocalLayerSettings();
    public TrailLayerSettings trailLayer = new TrailLayerSettings();

    private const string LocalChildName = "Mist_Local";
    private const string TrailChildName = "Mist_Trail";

    private ParticleSystem localPs;
    private ParticleSystem trailPs;

    private ParticleSystemRenderer localRenderer;
    private ParticleSystemRenderer trailRenderer;

    private bool initialized;
    private bool emissionEnabled = true;

    private Vector3 lastWorldPosition;
    private float smoothedSpeed;
    private float trailEmissionBlend;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();

        if (Application.isPlaying)
        {
            if (shared.playOnStart)
                PlayMist();
            else
                StopMist(true);
        }
    }

    private void OnValidate()
    {
        Initialize();
        BuildOrUpdate();
    }

    private void Update()
    {
        if (!Application.isPlaying || !initialized)
            return;

        UpdateMovementBasedTrail();
    }

    [ContextMenu("Build / Update Mist")]
    public void BuildOrUpdate()
    {
        EnsureSystems();

        ConfigureLocalSystem();
        ConfigureTrailSystem();

        if (!Application.isPlaying)
        {
            if (localPs != null) localPs.Clear();
            if (trailPs != null) trailPs.Clear();
        }
    }

    public void PlayMist()
    {
        Initialize();
        emissionEnabled = true;

        if (localPs != null && localLayer.enabled)
        {
            SetEmissionEnabled(localPs, true);
            localPs.Play(true);
        }

        if (trailPs != null && trailLayer.enabled)
        {
            SetEmissionEnabled(trailPs, true);
            trailPs.Play(true);
        }
    }

    public void StopMist(bool clear = false)
    {
        emissionEnabled = false;
        trailEmissionBlend = 0f;
        smoothedSpeed = 0f;

        if (localPs != null)
        {
            SetEmissionEnabled(localPs, false);
            localPs.Stop(true, clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
        }

        if (trailPs != null)
        {
            SetTrailRate(0f);
            SetEmissionEnabled(trailPs, false);
            trailPs.Stop(true, clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public void ClearMist()
    {
        if (localPs != null) localPs.Clear(true);
        if (trailPs != null) trailPs.Clear(true);
    }

    public void SetEmissionEnabled(bool enabled)
    {
        if (enabled) PlayMist();
        else StopMist(false);
    }

    public void SetLocalEmissionEnabled(bool enabled)
    {
        if (localPs == null) return;

        localLayer.enabled = enabled;

        if (enabled && emissionEnabled)
        {
            SetEmissionEnabled(localPs, true);
            localPs.Play(true);
        }
        else
        {
            SetEmissionEnabled(localPs, false);
            localPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public void SetTrailEmissionEnabled(bool enabled)
    {
        if (trailPs == null) return;

        trailLayer.enabled = enabled;

        if (enabled && emissionEnabled)
        {
            SetEmissionEnabled(trailPs, true);
            trailPs.Play(true);
        }
        else
        {
            trailEmissionBlend = 0f;
            SetTrailRate(0f);
            SetEmissionEnabled(trailPs, false);
            trailPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void Initialize()
    {
        EnsureSystems();
        BuildOrUpdate();

        lastWorldPosition = transform.position;
        smoothedSpeed = 0f;
        trailEmissionBlend = 0f;

        initialized = true;
    }

    private void UpdateMovementBasedTrail()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        Vector3 currentPosition = transform.position;
        float currentSpeed = (currentPosition - lastWorldPosition).magnitude / dt;
        lastWorldPosition = currentPosition;

        // Плавно сглаживаем скорость, чтобы хвост не дергался от микроколебаний.
        float speedLerp = 1f - Mathf.Exp(-trailLayer.speedSmoothing * dt);
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, currentSpeed, speedLerp);

        float targetBlend = 0f;

        if (emissionEnabled && trailLayer.enabled && smoothedSpeed > trailLayer.minSpeedToEmit)
            targetBlend = 1f;

        trailEmissionBlend = Mathf.MoveTowards(
            trailEmissionBlend,
            targetBlend,
            trailLayer.emissionBlendSpeed * dt
        );

        if (trailPs != null)
        {
            SetEmissionEnabled(trailPs, true);
            SetTrailRate(trailLayer.particlesPerSecond * trailEmissionBlend);

            if (!trailPs.isPlaying)
                trailPs.Play(true);
        }
    }

    private void SetTrailRate(float rate)
    {
        if (trailPs == null) return;

        var emission = trailPs.emission;
        emission.rateOverTime = rate;
    }

    private void EnsureSystems()
    {
        localPs = GetOrCreateParticleSystem(LocalChildName, out localRenderer);
        trailPs = GetOrCreateParticleSystem(TrailChildName, out trailRenderer);
    }

    private ParticleSystem GetOrCreateParticleSystem(string childName, out ParticleSystemRenderer renderer)
    {
        Transform child = transform.Find(childName);
        GameObject go;

        if (child == null)
        {
            go = new GameObject(childName);
            go.transform.SetParent(transform, false);
        }
        else
        {
            go = child.gameObject;
        }

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        if (ps == null) ps = go.AddComponent<ParticleSystem>();

        renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();

        return ps;
    }

    private void ConfigureLocalSystem()
    {
        if (localPs == null) return;

        var main = localPs.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = localLayer.maxParticles;
        main.startLifetime = new ParticleSystem.MinMaxCurve(localLayer.lifetimeRange.x, localLayer.lifetimeRange.y);
        main.startSize = new ParticleSystem.MinMaxCurve(localLayer.startSizeRange.x, localLayer.startSizeRange.y);
        main.startSpeed = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        if (shared.alwaysSimulateOffscreen)
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = localPs.emission;
        emission.enabled = localLayer.enabled;
        emission.rateOverTime = localLayer.particlesPerSecond;
        emission.rateOverDistance = 0f;

        var shape = localPs.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = shared.boxSize;
        shape.randomDirectionAmount = 0f;
        shape.sphericalDirectionAmount = 0f;

        DisableDynamicMovementModules(localPs);

        var colorOverLifetime = localPs.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateAlphaGradient(localLayer.peakAlpha));

        var sizeOverLifetime = localPs.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateSizeCurve(0.75f, 0.95f, 1.10f));

        ConfigureRenderer(localRenderer, shared.particleMaterial);
    }

    private void ConfigureTrailSystem()
    {
        if (trailPs == null) return;

        var main = trailPs.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = trailLayer.maxParticles;
        main.startLifetime = new ParticleSystem.MinMaxCurve(trailLayer.lifetimeRange.x, trailLayer.lifetimeRange.y);
        main.startSize = new ParticleSystem.MinMaxCurve(trailLayer.startSizeRange.x, trailLayer.startSizeRange.y);
        main.startSpeed = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        if (shared.alwaysSimulateOffscreen)
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = trailPs.emission;
        emission.enabled = trailLayer.enabled;
        emission.rateOverTime = 0f; // стартуем с нуля, дальше Update плавно поднимает
        emission.rateOverDistance = 0f;

        var shape = trailPs.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = shared.boxSize;
        shape.randomDirectionAmount = 0f;
        shape.sphericalDirectionAmount = 0f;

        DisableDynamicMovementModules(trailPs);

        if (Mathf.Abs(trailLayer.downwardVelocity) > 0.0001f)
        {
            var velocity = trailPs.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = trailLayer.downwardVelocity;
        }

        var colorOverLifetime = trailPs.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateAlphaGradient(trailLayer.peakAlpha));

        var sizeOverLifetime = trailPs.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateSizeCurve(0.80f, 0.98f, 1.08f));

        ConfigureRenderer(trailRenderer, shared.particleMaterial);
    }

    private void DisableDynamicMovementModules(ParticleSystem ps)
    {
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = false;

        var noise = ps.noise;
        noise.enabled = false;

        var force = ps.forceOverLifetime;
        force.enabled = false;

        var limitVelocity = ps.limitVelocityOverLifetime;
        limitVelocity.enabled = false;

        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = false;

        var inheritVelocity = ps.inheritVelocity;
        inheritVelocity.enabled = false;
    }

    private void ConfigureRenderer(ParticleSystemRenderer renderer, Material material)
    {
        if (renderer == null) return;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;

        if (material != null)
            renderer.sharedMaterial = material;
    }

    private void SetEmissionEnabled(ParticleSystem ps, bool enabled)
    {
        if (ps == null) return;
        var emission = ps.emission;
        emission.enabled = enabled;
    }

    private Gradient CreateAlphaGradient(float peakAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.00f, 0.00f),
                new GradientAlphaKey(peakAlpha, 0.18f),
                new GradientAlphaKey(peakAlpha, 0.75f),
                new GradientAlphaKey(0.00f, 1.00f)
            }
        );
        return gradient;
    }

    private AnimationCurve CreateSizeCurve(float a, float b, float c)
    {
        return new AnimationCurve(
            new Keyframe(0.00f, a),
            new Keyframe(0.25f, b),
            new Keyframe(1.00f, c)
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.35f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, shared.boxSize);
        Gizmos.matrix = oldMatrix;
    }
}