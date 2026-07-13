using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class VRTriggerPokeProvider : MonoBehaviour
{
    [Header("Left Hand")]
    public Transform leftPokePoint;
    public InputActionProperty leftTrigger;

    [Header("Right Hand")]
    public Transform rightPokePoint;
    public InputActionProperty rightTrigger;

    // C#-событие, которое передает Transform руки (кончика пальца), нажавшей триггер
    public event Action<Transform> OnTriggerPulled;

    private void OnEnable()
    {
        // Обязательно включаем экшены и подписываемся на события performed
        if (leftTrigger.action != null)
        {
            leftTrigger.action.Enable();
            leftTrigger.action.performed += OnLeftTriggerPerformed;
        }

        if (rightTrigger.action != null)
        {
            rightTrigger.action.Enable();
            rightTrigger.action.performed += OnRightTriggerPerformed;
        }
    }

    private void OnDisable()
    {
        // Отписываемся при выключении объекта, чтобы избежать утечек памяти
        if (leftTrigger.action != null)
        {
            leftTrigger.action.performed -= OnLeftTriggerPerformed;
        }

        if (rightTrigger.action != null)
        {
            rightTrigger.action.performed -= OnRightTriggerPerformed;
        }
    }

    private void OnLeftTriggerPerformed(InputAction.CallbackContext context)
    {
        // Если левый триггер нажат, вызываем событие и передаем левый палец
        if (leftPokePoint != null) OnTriggerPulled?.Invoke(leftPokePoint);
    }

    private void OnRightTriggerPerformed(InputAction.CallbackContext context)
    {
        // Если правый триггер нажат, вызываем событие и передаем правый палец
        if (rightPokePoint != null) OnTriggerPulled?.Invoke(rightPokePoint);
    }
}