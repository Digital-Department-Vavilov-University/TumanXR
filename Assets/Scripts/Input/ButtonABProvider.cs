using UnityEngine;
using UnityEngine.InputSystem;

public class ButtonABProvider : MonoBehaviour
{
    [Header("Input Actions")]
    [Tooltip("Ссылка на действие (Action) для кнопки A")]
    [SerializeField] private InputActionProperty buttonA;
    
    [Tooltip("Ссылка на действие (Action) для кнопки B")]
    [SerializeField] private InputActionProperty buttonB;

    // Публичные свойства, которые будут читать другие скрипты
    public bool IsButtonAPressed => buttonA.action != null && buttonA.action.IsPressed();
    public bool IsButtonBPressed => buttonB.action != null && buttonB.action.IsPressed();
}