using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Абстрактный класс, от которого наследуются все провайдеры ввода для транспорта.
    /// Он обязан уметь вернуть Steering и Throttle.
    /// Дополнительно, если нужно, можно объявить события/делегаты.
    /// </summary>
    public abstract class VehicleInputProvider : MonoBehaviour
    {
        /// <summary>
        /// Значение рулевого управления [-1..1].
        /// </summary>
        public abstract float GetSteering();

        /// <summary>
        /// Значение газа/тормоза ([-1..1], где -1 = задний ход/торможение, +1 = газ).
        /// </summary>
        public abstract float GetThrottle();
    }
}
