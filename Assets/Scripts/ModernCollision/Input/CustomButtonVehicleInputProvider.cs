using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Провайдер ввода, использующий кастомно назначенные кнопки для управления транспортным средством.
    /// Назначьте кнопки для движения вперёд, назад, а также для поворота влево и вправо через инспектор.
    /// </summary>
    public class CustomButtonVehicleInputProvider : VehicleInputProvider
    {
        [Tooltip("Кнопка для движения вперёд")]
        public KeyCode forwardKey = KeyCode.W;

        [Tooltip("Кнопка для движения назад")]
        public KeyCode backwardKey = KeyCode.S;

        [Tooltip("Кнопка для поворота влево")]
        public KeyCode leftKey = KeyCode.A;

        [Tooltip("Кнопка для поворота вправо")]
        public KeyCode rightKey = KeyCode.D;

        /// <summary>
        /// Возвращает значение управления поворотом:
        /// -1, если нажата кнопка для поворота влево, 1 — для поворота вправо, 0 — если кнопки не нажаты или нажаты обе.
        /// </summary>
        public override float GetSteering()
        {
            float steering = 0f;

            if (Input.GetKey(leftKey))
            {
                steering -= 1f;
            }
            if (Input.GetKey(rightKey))
            {
                steering += 1f;
            }

            // Гарантируем, что значение находится в диапазоне от -1 до 1
            return Mathf.Clamp(steering, -1f, 1f);
        }

        /// <summary>
        /// Возвращает значение управления ускорением:
        /// 1, если нажата кнопка для движения вперёд, -1 — для движения назад, 0 — если кнопки не нажаты или нажаты обе.
        /// </summary>
        public override float GetThrottle()
        {
            float throttle = 0f;

            if (Input.GetKey(forwardKey))
            {
                throttle += 1f;
            }
            if (Input.GetKey(backwardKey))
            {
                throttle -= 1f;
            }

            // Гарантируем, что значение находится в диапазоне от -1 до 1
            return Mathf.Clamp(throttle, -1f, 1f);
        }
    }
}
