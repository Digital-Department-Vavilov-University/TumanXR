using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Управляет UI-уведомлением для подтверждения сброса симуляции.
/// </summary>
public class ResetNotificationManager : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionReference toggleNotificationAction;
    public InputActionReference confirmResetAction;

    [Header("UI Elements")]
    public GameObject notificationUI;
    public float smoothSpeed = 5f;

    [Header("Dependencies")]
    [Tooltip("Ссылка на наш главный менеджер сброса")]
    public MasterResetManager masterResetManager;

    private Transform cameraTransform;
    private bool isNotificationVisible = false;

    private void OnEnable()
    {
        if (toggleNotificationAction != null) toggleNotificationAction.action.performed += ToggleNotification;
        if (confirmResetAction != null) confirmResetAction.action.performed += ConfirmReset;
    }

    private void OnDisable()
    {
        if (toggleNotificationAction != null) toggleNotificationAction.action.performed -= ToggleNotification;
        if (confirmResetAction != null) confirmResetAction.action.performed -= ConfirmReset;
    }

    private void Start()
    {
        ValidateComponents();
        
        // Кэшируем камеру, чтобы не искать её каждый кадр
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        
        SetNotificationVisibility(false);
    }

    private void Update()
    {
        // Плавно перемещаем UI за взглядом пользователя, если меню открыто
        if (isNotificationVisible)
        {
            MoveUIToCamera();
        }
    }

    /// <summary>
    /// Включает/выключает меню подтверждения сброса.
    /// </summary>
    private void ToggleNotification(InputAction.CallbackContext context)
    {
        SetNotificationVisibility(!isNotificationVisible);
    }

    /// <summary>
    /// Выполняет сброс, если меню открыто и игрок нажал кнопку подтверждения.
    /// </summary>
    private void ConfirmReset(InputAction.CallbackContext context)
    {
        if (isNotificationVisible)
        {
            if (masterResetManager != null)
            {
                // Вызываем тот самый публичный метод из нашего MasterResetManager
                masterResetManager.ResetAllSystems();
                
                // Прячем UI после успешного сброса
                SetNotificationVisibility(false);
            }
            else
            {
                Debug.LogError("[ResetNotificationManager] MasterResetManager не назначен!");
            }
        }
    }

    /// <summary>
    /// Управляет состоянием активности UI объекта.
    /// </summary>
    private void SetNotificationVisibility(bool isVisible)
    {
        isNotificationVisible = isVisible;
        
        if (notificationUI != null)
        {
            notificationUI.SetActive(isVisible);
        }

        // Если только что включили меню, моментально телепортируем его к камере,
        // чтобы оно не "вылетало" откуда-то сбоку
        if (isVisible)
        {
            MoveUIToCamera(true);
        }
    }

    /// <summary>
    /// Перемещает и поворачивает UI перед камерой (для VR).
    /// </summary>
    private void MoveUIToCamera(bool instant = false)
    {
        if (cameraTransform == null || notificationUI == null) return;

        // Позиция: 2 метра прямо перед лицом
        Vector3 targetPosition = cameraTransform.position + cameraTransform.forward * 2f;
        // Поворот: чтобы UI "смотрел" ровно на камеру
        Quaternion targetRotation = Quaternion.LookRotation(notificationUI.transform.position - cameraTransform.position);

        if (instant)
        {
            notificationUI.transform.position = targetPosition;
            notificationUI.transform.rotation = targetRotation;
        }
        else
        {
            notificationUI.transform.position = Vector3.Lerp(notificationUI.transform.position, targetPosition, smoothSpeed * Time.deltaTime);
            notificationUI.transform.rotation = Quaternion.Slerp(notificationUI.transform.rotation, targetRotation, smoothSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Проверяет, всё ли настроено в инспекторе.
    /// </summary>
    private void ValidateComponents()
    {
        if (notificationUI == null)
            Debug.LogWarning("[ResetNotificationManager] UI объект уведомления (notificationUI) не назначен в инспекторе!");

        if (toggleNotificationAction == null || confirmResetAction == null)
            Debug.LogError("[ResetNotificationManager] Input Actions для кнопок не назначены!");

        if (masterResetManager == null)
            Debug.LogError("[ResetNotificationManager] MasterResetManager не назначен в инспекторе!");
    }
}