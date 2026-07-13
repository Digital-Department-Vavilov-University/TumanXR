using UnityEngine;
using MyVehicleSystem.ModernCollision;

/// <summary>
/// Простой менеджер для управления одним транспортным средством.
/// </summary>
public class SingleVehicleManager : MonoBehaviour
{
    [Header("Vehicle")]
    [Tooltip("Драйвер транспорта, которым управляет этот менеджер.")]
    public AdvancedCarDriver driver;

    // -------------------------
    // Saved start pose
    // -------------------------

    private Vector3 startCarPos;
    private Quaternion startCarRot;

    private Vector3 startTrailerPos;
    private Quaternion startTrailerRot;
    private bool hasTrailer;

    private void Awake()
    {
        if (driver == null)
        {
            Debug.LogError(
                "SingleVehicleManager: AdvancedCarDriver не назначен!",
                this
            );
            enabled = false;
        }
    }

    private void Start()
    {
        if (driver == null || driver.car == null)
        {
            Debug.LogError("SingleVehicleManager: Невозможно сохранить стартовую позу — car == null.");
            enabled = false;
            return;
        }

        // Сохраняем стартовую позу машины
        startCarPos = driver.car.transform.position;
        startCarRot = driver.car.transform.rotation;

        // Сохраняем стартовую позу прицепа (если есть)
        hasTrailer = driver.trailer != null;
        if (hasTrailer)
        {
            startTrailerPos = driver.trailer.transform.position;
            startTrailerRot = driver.trailer.transform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (driver == null) return;
        Tick(Time.fixedDeltaTime);
    }

    private void Tick(float dt)
    {
        driver.TickFixedUpdate(
            dt,
            out _, out _,
            out _,
            out _, out _, out _,
            out _,
            out _, out _, out _,
            out _,
            out _, out _
        );
    }

    // -------------------------
    // Reset API
    // -------------------------

    /// <summary>
    /// Сброс транспорта строго в стартовую позу.
    /// </summary>
    public void ResetVehicle()
    {
        if (driver == null || driver.car == null)
            return;

        driver.car.TeleportAndResetState(startCarPos, startCarRot);

        if (hasTrailer && driver.trailer != null)
        {
            driver.trailer.TeleportAndResetState(
                startTrailerPos,
                startTrailerRot
            );
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        const float w = 220f;
        const float h = 40f;
        const float pad = 10f;

        Rect r = new Rect(pad, pad, w, h);
        if (GUI.Button(r, "Reset Vehicle"))
        {
            ResetVehicle();
        }
    }
#endif
}
