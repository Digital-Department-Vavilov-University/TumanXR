using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Итеративно решает хич-констрейнт между машиной и прицепом.
    /// При распределении корректировки используется несколько итераций, чтобы ошибка сцепки распределялась постепенно.
    /// Это помогает уменьшить осцилляцию (дрожание) при столкновениях, особенно в сложных ситуациях (например, задним ходом, когда прицеп упёрся в стену).
    /// </summary>
    public class CarTrailerConstraintSolver : MonoBehaviour
    {
        [Header("Hitch Constraint Settings")]
        [Tooltip("Доля ошибки, компенсируемая за счёт корректировки положения машины (0..1). Если 1 – вся корректировка идёт за счёт машины, если 0 – за счёт прицепа.")]
        [Range(0f, 1f)]
        public float hitchStiffness = 0.5f;
        [Tooltip("Порог ошибки (в метрах), ниже которого коррекция не применяется")]
        public float hitchErrorThreshold = 0.05f;
        [Tooltip("Максимальное число итераций для решения констрейнта")]
        public int solverIterations = 5;
        [Tooltip("Коэффициент демпфирования корректировки (0..1), где 1 означает отсутствие дополнительного сглаживания")]
        [Range(0f, 1f)]
        public float correctionDamping = 0.8f;

        /// <summary>
        /// Итеративно решает хич-констрейнт между машиной и прицепом.
        /// Принимает текущие позиции и повороты машины и прицепа, а также локальные точки сцепки.
        /// Возвращает скорректированные состояния (позиции и повороты).
        /// </summary>
        public (Vector3, Quaternion, Vector3, Quaternion) SolveHitchConstraint(
            Vector3 carPose, Quaternion carRot,
            Vector3 trailerPose, Quaternion trailerRot,
            Vector3 carHitchLocal, Vector3 trailerHitchLocal)
        {
            // Начинаем с исходных позиций (вращения пока не корректируем)
            Vector3 newCarPos = carPose;
            Vector3 newTrailerPos = trailerPose;
            Quaternion newCarRot = carRot;
            Quaternion newTrailerRot = trailerRot;

            for (int i = 0; i < solverIterations; i++)
            {
                // Вычисляем мировые позиции точек сцепки
                Vector3 carHitchWorld = newCarPos + newCarRot * carHitchLocal;
                Vector3 trailerHitchWorld = newTrailerPos + newTrailerRot * trailerHitchLocal;

                // Ошибка констрейнта – вектор между точками сцепки
                Vector3 error = trailerHitchWorld - carHitchWorld;
                float errorMag = error.magnitude;
                if (errorMag < hitchErrorThreshold)
                {
                    // Если ошибка уже мала, завершаем итерации
                    break;
                }

                // Вычисляем корректировку на данном шаге:
                // Для сглаживания берем лишь часть ошибки, распределенную на solverIterations
                Vector3 correction = error * correctionDamping / solverIterations;

                // Корректировка распределяется между машиной и прицепом:
                // hitchStiffness определяет долю, компенсируемую машиной.
                newCarPos += hitchStiffness * correction;
                newTrailerPos -= (1f - hitchStiffness) * correction;
            }

            return (newCarPos, newCarRot, newTrailerPos, newTrailerRot);
        }
    }
}
