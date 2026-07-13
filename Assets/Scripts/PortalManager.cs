using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class PortalManager : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Основная камера игрока из XR Origin")]
    public Camera playerCamera;
    [Tooltip("Якорь внутри кабины-копии, соответствующей этому транспорту")]
    public Transform playerAnchor;
    [Tooltip("Якорь на этом (реальном) транспорте")]
    public Transform remoteAnchor;

    [Header("Настройки")]
    [Tooltip("Дальность обзора камеры игрока, когда он в этой кабине")]
    public float cabinFarClipPlane = 15f;

    // Приватные переменные
    private Camera remoteCamera;
    private UniversalAdditionalCameraData _playerCameraData;
    private UniversalAdditionalCameraData _remoteCameraData;
    
    private float _defaultFarClipPlane;
    private bool _isRenderingActive = false;

    void Awake()
    {
        // Получаем ссылки на компоненты камер для работы с URP
        remoteCamera = GetComponent<Camera>();
        playerCamera.TryGetComponent(out _playerCameraData);
        remoteCamera.TryGetComponent(out _remoteCameraData);

        // Убеждаемся, что при старте камера выключена
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += SyncCameras;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= SyncCameras;
    }

    /// <summary>
    /// Включает рендеринг портала для этого транспорта.
    /// </summary>
    public void EnablePortalRendering()
    {
        // Сохраняем стандартную дальность обзора игрока
        _defaultFarClipPlane = playerCamera.farClipPlane;

        // Включаем этот объект (и привязанную к нему камеру)
        gameObject.SetActive(true);

        // Настраиваем рендеринг
        _remoteCameraData.renderType = CameraRenderType.Base;
        _playerCameraData.renderType = CameraRenderType.Overlay;
        _remoteCameraData.cameraStack.Add(playerCamera);
        playerCamera.farClipPlane = cabinFarClipPlane;

        _isRenderingActive = true;
    }

    /// <summary>
    /// Выключает рендеринг портала и возвращает камеры в исходное состояние.
    /// </summary>
    public void DisablePortalRendering()
    {
        _isRenderingActive = false;

        // Возвращаем настройки камеры игрока
        _playerCameraData.renderType = CameraRenderType.Base;
        playerCamera.farClipPlane = _defaultFarClipPlane;

        // Очищаем стек и выключаем камеру транспорта
        _remoteCameraData.cameraStack.Clear();
        gameObject.SetActive(false);
    }
    
    private void SyncCameras(ScriptableRenderContext context, Camera camera)
    {
        // Выполняем синхронизацию, только если рендеринг активен и это наша камера
        if (!_isRenderingActive || camera != remoteCamera)
        {
            return;
        }

        Vector3 localOffset = playerCamera.transform.position - playerAnchor.position;
        Quaternion localRotationOffset = Quaternion.Inverse(playerAnchor.rotation) * playerCamera.transform.rotation;

        remoteCamera.transform.position = remoteAnchor.position + (remoteAnchor.rotation * localOffset);
        remoteCamera.transform.rotation = remoteAnchor.rotation * localRotationOffset;
    }
}