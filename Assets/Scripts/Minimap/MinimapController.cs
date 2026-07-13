using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [Header("Targeting")]
    public Transform target;
    public BoomController boomController;
    
    [Header("Camera Settings")]
    public Camera minimapCamera;
    public float heightOffset = 50f;
    public float minimapZoom = 15f;

    [Header("Pointer Auto-Scaling")]
    public float referenceZoom = 15f;
    public Vector2 referencePointerSize = new Vector2(300f, 150f);
    public Vector2 referencePointerPos = new Vector2(0f, -15f);

    [Header("Rotation Mode")]
    public bool rotateWithTarget = false;

    [Header("UI Pointer Visuals")]
    public RectTransform playerIndicator;
    public Image pointerImage; 
    
    public Sprite state1Folded;
    public Sprite state2BoomA;
    public Sprite state3BoomB;
    public Sprite state4BaseLowered;

    [Header("Blinking (Transition)")]
    [Tooltip("Обычный цвет (обычно просто белый)")]
    public Color normalColor = Color.white;
    [Tooltip("Цвет во время движения механизма (например, желтый или оранжевый)")]
    public Color transitionColor = new Color(1f, 0.8f, 0f, 1f); // Желтый цвет по умолчанию
    [Tooltip("Скорость пульсации цвета")]
    public float blinkSpeed = 6f;

    private BoomController.MechanismState lastKnownState;

    private void Start()
    {
        if (minimapCamera != null && !minimapCamera.orthographic) minimapCamera.orthographic = true;

        if (boomController != null)
        {
            lastKnownState = boomController.CurrentState;
            UpdatePointerSprite(lastKnownState);
        }
    }

    private void LateUpdate()
    {
        if (target == null || minimapCamera == null) return;

        // 1. Позиция камеры в мире
        Vector3 newPosition = target.position;
        newPosition.y = target.position.y + heightOffset;
        transform.position = newPosition;
        
        // 2. Зум камеры
        minimapCamera.orthographicSize = minimapZoom;

        // 3. Авто-масштабирование указателя
        if (playerIndicator != null && referenceZoom > 0f)
        {
            float scaleRatio = referenceZoom / minimapZoom;
            playerIndicator.sizeDelta = referencePointerSize * scaleRatio;
            playerIndicator.anchoredPosition = referencePointerPos * scaleRatio;
        }

        // 4. Логика вращения указателя
        if (rotateWithTarget)
        {
            transform.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            if (playerIndicator != null) playerIndicator.localRotation = Quaternion.identity;
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            if (playerIndicator != null) playerIndicator.localRotation = Quaternion.Euler(0f, 0f, -target.eulerAngles.y);
        }

        // 5. Логика смены спрайтов (опрос состояния)
        if (boomController != null && pointerImage != null)
        {
            BoomController.MechanismState currentState = boomController.CurrentState;
            if (currentState != lastKnownState)
            {
                lastKnownState = currentState;
                UpdatePointerSprite(currentState);
            }

            // 6. ЛОГИКА МИГАНИЯ ПРИ ДВИЖЕНИИ
            if (boomController.IsTransitioning)
            {
                // Используем синусоиду для плавного перехода от 0 до 1 туда-сюда
                float pingPong = (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f;
                pointerImage.color = Color.Lerp(normalColor, transitionColor, pingPong);
            }
            else
            {
                // Если движение закончено, жестко возвращаем обычный цвет
                pointerImage.color = normalColor;
            }
        }
    }

    private void UpdatePointerSprite(BoomController.MechanismState state)
    {
        switch (state)
        {
            case BoomController.MechanismState.Folded: pointerImage.sprite = state1Folded; break;
            case BoomController.MechanismState.BoomA: pointerImage.sprite = state2BoomA; break;
            case BoomController.MechanismState.BoomB: pointerImage.sprite = state3BoomB; break;
            case BoomController.MechanismState.BaseLowered: pointerImage.sprite = state4BaseLowered; break;
        }
    }
}