using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    public class AdvancedCarDriver : MonoBehaviour
    {
        [Tooltip("Источник ввода для управления машиной.")]
        public VehicleInputProvider input;

        [Header("Car")]
        [Tooltip("Кинематический контроллер машины (4w / 6w / etc).")]
        [SerializeField] private MonoBehaviour carController;

        [Tooltip("Компонент для кинематического разрешения столкновений машины.")]
        public AdvancedVehicleCollisionResolver carCollisionResolver;

        [Header("Trailer")]
        public AdvancedTrailerController trailer;

        [Tooltip("Компонент для кинематического разрешения столкновений для прицепа")]
        public AdvancedVehicleCollisionResolver trailerCollisionResolver;

        [Tooltip("Компонент для решения хич-констрейнта между машиной и прицепом")]
        public CarTrailerConstraintSolver hitchSolver;

        // --- ПОЛИМОРФНЫЙ КОНТРОЛЛЕР МАШИНЫ ---
        private ICarKinematicController _car;

        /// <summary>
        /// Публичный read-only доступ к контроллеру машины.
        /// Используется VehicleInterpolator / NetworkVehicleManager.
        /// </summary>
        public ICarKinematicController car
        {
            get
            {
                if (_car != null) return _car;

                // 1) Попытка взять из назначенного в инспекторе MonoBehaviour
                if (carController != null)
                {
                    _car = carController as ICarKinematicController;
                }

                // 2) Если не получилось — попробуем найти компонент на том же GameObject
                if (_car == null)
                {
                    _car = GetComponent<ICarKinematicController>();
                }

                // 3) Если всё ещё не найден — ищем в children (часто контроллер - дочерний)
                if (_car == null)
                {
                    _car = GetComponentInChildren<ICarKinematicController>();
                }

                // 4) Если не найден — логируем и бросаем понятную ошибку
                if (_car == null)
                {
                    string message = $"AdvancedCarDriver on '{gameObject.name}' requires an ICarKinematicController. " +
                                    "Assign carController in inspector or add a component implementing ICarKinematicController.";
                    Debug.LogError(message, this);
                    throw new System.InvalidOperationException(message);
                }

                return _car;
            }
        }

        void Awake()
        {
            var _ = car;
        }

        void FixedUpdate()
        {
            // float dt = Time.fixedDeltaTime;
            // TickFixedUpdate(dt);
        }

        /// <summary>
        /// Основной метод обновления состояния машины и прицепа.
        /// </summary>
        public void TickFixedUpdate(
            float dt,
            out Vector3 finalCarPosition, out Quaternion finalCarRotation,
            out bool trailerExistsAndWasProcessed,
            out Vector3 finalTrailerPosition, out Quaternion finalTrailerRotation, out float finalAppliedTrailerAngleDeg,
            out float outCarVisualSteerDeg,
            out float outCarFLRollDeg, out float outCarFRRollDeg,
            out float outCarRLRollDeg, out float outCarRRRollDeg,
            out float outTrailerLFRollDeg,
            out float outTrailerRFRollDeg
        )
        {
            // --- I. Значения по умолчанию ---
            finalCarPosition = Vector3.zero;
            finalCarRotation = Quaternion.identity;
            trailerExistsAndWasProcessed = false;

            finalTrailerPosition = Vector3.zero;
            finalTrailerRotation = Quaternion.identity;
            finalAppliedTrailerAngleDeg = 0f;

            outCarVisualSteerDeg = 0f;
            outCarFLRollDeg = 0f;
            outCarFRRollDeg = 0f;
            outCarRLRollDeg = 0f;
            outCarRRRollDeg = 0f;

            outTrailerLFRollDeg = 0f;
            outTrailerRFRollDeg = 0f;

            // --- II. Проверки ---
            if (_car == null || input == null || carCollisionResolver == null)
            {
                Debug.LogError(
                    "AdvancedCarDriver: car, input или carCollisionResolver не назначены!",
                    this
                );
                return;
            }

            // --- III. МАШИНА ---
            float steerInput = input.GetSteering();
            float throttleInput = input.GetThrottle();

            // 1. Обновление динамики
            _car.UpdateDynamics(dt, steerInput, throttleInput);

            // 2. Расчет желаемой позы
            var (desiredCarPos, desiredCarRot) = _car.CalculateFuturePose(dt);

            // 3. Текущая поза
            Transform carTransform = _car.transform;
            carTransform.GetPositionAndRotation(
                out Vector3 currentCarPos,
                out Quaternion currentCarRot
            );

            // 4. Коллизии
            MoveResult carMoveResult =
                carCollisionResolver.Move(
                    currentCarPos,
                    currentCarRot,
                    desiredCarPos,
                    desiredCarRot
                );

            // 5. Применение результата
            _car.ApplyPose(
                carMoveResult.FinalPosition,
                carMoveResult.FinalRotation
            );

            // 6. Feedback от коллизий
            _car.ApplyCollisionFeedback(carMoveResult);

            // 7. Визуалы колес
            _car.UpdateWheelVisuals(dt);

            // 8. Out-параметры машины
            finalCarPosition = carMoveResult.FinalPosition;
            finalCarRotation = carMoveResult.FinalRotation;

            outCarFLRollDeg = _car.GetFLWheelRollDeg();
            outCarFRRollDeg = _car.GetFRWheelRollDeg();
            outCarRLRollDeg = _car.GetRLWheelRollDeg();
            outCarRRRollDeg = _car.GetRRWheelRollDeg();
            outCarVisualSteerDeg = _car.GetVisualSteerAngleDeg();

            // --- IV. ПРИЦЕП ---
            if (trailer == null || trailer.carHitch == null || trailer.trailerHitch == null ||
                trailerCollisionResolver == null || hitchSolver == null)
            {
                return;
            }

            // Будущая поза сцепки машины
            Transform carHitch = trailer.carHitch;
            Vector3 hitchLocalPos = carHitch.localPosition;
            Quaternion hitchLocalRot = carHitch.localRotation;

            Vector3 futureCarHitchPos =
                carMoveResult.FinalPosition +
                carMoveResult.FinalRotation * hitchLocalPos;

            Quaternion futureCarHitchRot =
                carMoveResult.FinalRotation * hitchLocalRot;

            float futureCarAngleY = futureCarHitchRot.eulerAngles.y;

            // 1. Будущая поза прицепа
            var (desiredTrailerPos, desiredTrailerRot, futureTrailerAngle) =
                trailer.ComputeFuturePose(
                    dt,
                    futureCarHitchPos,
                    futureCarAngleY
                );

            trailer.transform.GetPositionAndRotation(
                out Vector3 currentTrailerPos,
                out Quaternion currentTrailerRot
            );

            // 2. Коллизии прицепа
            MoveResult trailerMoveResult =
                trailerCollisionResolver.Move(
                    currentTrailerPos,
                    currentTrailerRot,
                    desiredTrailerPos,
                    desiredTrailerRot
                );

            // 3. Хич-констрейнт
            Vector3 carHitchLocalPos = trailer.carHitch.localPosition;
            Vector3 trailerHitchLocalPos = trailer.trailerHitch.localPosition;

            (Vector3 finalCarPos, Quaternion finalCarRot,
             Vector3 finalTrailerPos, Quaternion finalTrailerRot) =
                hitchSolver.SolveHitchConstraint(
                    carMoveResult.FinalPosition,
                    carMoveResult.FinalRotation,
                    trailerMoveResult.FinalPosition,
                    trailerMoveResult.FinalRotation,
                    carHitchLocalPos,
                    trailerHitchLocalPos
                );

            _car.ApplyPose(finalCarPos, finalCarRot);
            trailer.ApplyPose(finalTrailerPos, finalTrailerRot, futureTrailerAngle);

            trailer.UpdateWheelVisuals(dt);

            // 4. Финальные out-параметры
            finalCarPosition = finalCarPos;
            finalCarRotation = finalCarRot;
            trailerExistsAndWasProcessed = true;

            finalTrailerPosition = finalTrailerPos;
            finalTrailerRotation = finalTrailerRot;
            finalAppliedTrailerAngleDeg = futureTrailerAngle;

            outCarFLRollDeg = _car.GetFLWheelRollDeg();
            outCarFRRollDeg = _car.GetFRWheelRollDeg();
            outCarRLRollDeg = _car.GetRLWheelRollDeg();
            outCarRRRollDeg = _car.GetRRWheelRollDeg();
            outCarVisualSteerDeg = _car.GetVisualSteerAngleDeg();

            outTrailerLFRollDeg = trailer.GetTLWheelRollDeg();
            outTrailerRFRollDeg = trailer.GetTRWheelRollDeg();
        }

        /// <summary>
        /// Перегрузка для обратной совместимости.
        /// </summary>
        public void TickFixedUpdate(float dt)
        {
            TickFixedUpdate(
                dt,
                out _, out _,
                out _,
                out _, out _, out _,
                out _,
                out _, out _, out _,
                out _,
                out _, out _
            );
        }
    }
}
