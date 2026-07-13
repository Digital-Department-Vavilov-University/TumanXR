using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Простейший провайдер ввода: берёт оси из Unity Input Manager.
    /// Является MonoBehaviour, чтобы удобно работать в сцене.
    /// </summary>
    public class KeyboardVehicleInputProvider : VehicleInputProvider
    {
        [Tooltip("Название horizontal-оси в Input Manager, напр. 'Horizontal'")]
        public string horizontalAxis = "Horizontal";
        [Tooltip("Название vertical-оси в Input Manager, напр. 'Vertical'")]
        public string verticalAxis = "Vertical";

        public override float GetSteering()
        {
            return Input.GetAxis(horizontalAxis);
        }

        public override float GetThrottle()
        {
            return Input.GetAxis(verticalAxis);
        }
    }
}
