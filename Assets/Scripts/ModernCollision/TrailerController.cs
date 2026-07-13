using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Класс прицепа, почти тот же что и в LegacyCollision, но без собственного Update(),
    /// чтобы мы могли управлять логикой извне.
    /// Основные методы:
    ///  - ComputeFuturePose: считает будущую позицию/поворот (не применяя их).
    ///  - ApplyPose: реально ставит прицеп в нужное положение.
    /// </summary>
    public class TrailerController : MonoBehaviour
    {
        [Header("Hitch Points")]
        public Transform carHitch;      // Ссылка на точку сцепки на машине (внешний объект)
        public Transform trailerHitch;  // Точка сцепки у самого прицепа (дочерний объект)

        [Header("Trailer Geometry")]
        public float trailerLength = 5f;

        [Header("Angle Clamping")]
        public bool useAngleClamp = false;
        [Range(0f, 180f)]
        public float maxFoldAngle = 80f;

        // Внутреннее состояние
        private float _trailerAngle;      // Текущий угол прицепа (глобальный вокруг Y)
        private Vector3 _prevRootPos;     // Позиция корня прицепа в прошлом кадре
        private Quaternion _prevRootRot;  // Поворот корня прицепа в прошлом кадре
        private Vector3 _lastCarHitchPos; // Позиция сцепки машины в прошлом кадре

        // Инициализация.
        private void Awake()
        {
            if (!carHitch || !trailerHitch)
            {
                Debug.LogError("[TrailerController] carHitch / trailerHitch not assigned!");
                return;
            }

            // Инициализируем предыдущее состояние текущим
            _prevRootPos = transform.position;
            _prevRootRot = transform.rotation;
            _trailerAngle = transform.eulerAngles.y;
            _lastCarHitchPos = carHitch.position;
        }

        /// <summary>
        /// На основе:
        ///  - прошлого состояния (храним в полях _prevRootPos/Rot, _trailerAngle, _lastCarHitchPos)
        ///  - новой глобальной позиции сцепки машины (futureCarHitchPos)
        ///  - будущего угла машины по Y (futureCarY)
        ///  - dt
        /// рассчитываем будущие (position, rotation, trailerAngle) прицепа (не применяем).
        /// </summary>
        public (Vector3 futurePos, Quaternion futureRot, float futureAngle) ComputeFuturePose(
            float dt,
            Vector3 futureCarHitchPos,
            float futureCarY)
        {
            Vector3 pivotLocal = trailerHitch.localPosition;
            Vector3 pivotOld = _prevRootPos + _prevRootRot * pivotLocal;

            // Смотрим, как переместилась машина за dt
            Vector3 deltaPos = (futureCarHitchPos - _lastCarHitchPos);
            deltaPos.y = 0f;
            float speed = deltaPos.magnitude / dt;

            float newTrailerAngle = _trailerAngle;
            if (speed > 1e-5f)
            {
                // Определяем направление скорости шарнира
                float phi = Mathf.Atan2(deltaPos.x, deltaPos.z) * Mathf.Rad2Deg;

                // dPsi/dt = (v / L) * sin(phi - Psi)
                float phiMinusPsi = Mathf.Deg2Rad * (phi - _trailerAngle);
                float dPsi = (speed / trailerLength) * Mathf.Sin(phiMinusPsi);
                float deltaAngleDeg = dPsi * Mathf.Rad2Deg * dt;
                newTrailerAngle = _trailerAngle + deltaAngleDeg;

                // Ограничиваем, если надо
                if (useAngleClamp)
                {
                    float diff = Mathf.DeltaAngle(futureCarY, newTrailerAngle);
                    float clamped = Mathf.Clamp(diff, -maxFoldAngle, maxFoldAngle);
                    newTrailerAngle = futureCarY + clamped;
                }
            }

            float rotateThisFrame = newTrailerAngle - _trailerAngle;
            Quaternion rotationDelta = Quaternion.Euler(0f, rotateThisFrame, 0f);

            Quaternion newRot = _prevRootRot * rotationDelta;
            Vector3 dirOld = _prevRootPos - pivotOld;
            Vector3 dirNew = rotationDelta * dirOld;
            Vector3 rotatedPos = pivotOld + dirNew;

            // Смещаем, чтобы trailerHitch совпал с futureCarHitchPos
            Vector3 pivotNew = rotatedPos + newRot * pivotLocal;
            Vector3 offset = futureCarHitchPos - pivotNew;
            Vector3 newPos = rotatedPos + offset;

            return (newPos, newRot, newTrailerAngle);
        }

        /// <summary>
        /// Применяет рассчитанные (pos, rot, angle) к реальному transform,
        /// обновляет "предыдущее" состояние, чтобы всё было согласовано к следующему кадру.
        /// </summary>
        public void ApplyPose(Vector3 newPos, Quaternion newRot, float newAngle)
        {
            transform.SetPositionAndRotation(newPos, newRot);

            _prevRootPos = newPos;
            _prevRootRot = newRot;
            _trailerAngle = newAngle;

            // Обновляем _lastCarHitchPos
            Vector3 pivotLocal = trailerHitch.localPosition;
            Vector3 pivotWorld = newPos + (newRot * pivotLocal);
            _lastCarHitchPos = pivotWorld;
        }
    }
}
