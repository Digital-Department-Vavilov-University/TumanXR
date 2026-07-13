using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // Для Handles.Label
#endif

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Отвечает за хранение состояния и кинематику машинки:
    ///  - Углы поворота колёс (визуализация)
    ///  - Текущая скорость и угол руления (для динамики)
    ///  - Позиция, поворот (через transform)
    ///  - Расчёт будущего положения по текущему состоянию (CalculateFuturePose)
    ///  - Применение новой позы (ApplyPose)
    ///  - Обновление внутреннего состояния на основе ввода и эффектов (UpdateDynamics, ApplyCollisionFeedback)
    /// Класс сам не занимается чтением прямого ввода (Input) или непосредственным разрешением коллизий.
    /// </summary>
    public class AdvancedCarController : MonoBehaviour, ICarKinematicController
    {
        [Header("Wheel References (Visuals)")]
        public Transform frontLeftWheel;
        public Transform frontRightWheel;
        public Transform rearLeftWheel; // Заднее левое колесо машины
        public Transform rearRightWheel; // Заднее правое колесо машины

        // УДАЛЕНО или ЗАКОММЕНТИРОВАНО старое поле, чтобы избежать путаницы:
        // [Tooltip("Радиус колеса для корректного визуального вращения (УСТАРЕЛО, используйте front/rear WheelRadius)")]
        // public float wheelRadius = 0.35f; 

        [Header("Wheel Radii (for Visuals)")] // Новые поля для разных радиусов
        [Tooltip("Радиус передних колес для корректного визуального вращения.")]
        public float frontWheelRadius = 0.35f;
        [Tooltip("Радиус задних колес для корректного визуального вращения.")]
        public float rearWheelRadius = 0.35f;

        [Header("Car Geometry & Base Dynamics")]
        [Tooltip("Максимальный угол поворота колес (градусы) для руления.")]
        public float maxSteerAngle = 30f;
        [Tooltip("Скорость изменения фактического угла руля к целевому (градусы в секунду).")]
        public float steerSpeed = 90f;
        [Tooltip("L_v: Колесная база - расстояние между передней и задней осями.")]
        public float wheelBase = 2.5f;

        [Header("Speed & Acceleration")]
        [Tooltip("Максимальная скорость движения вперед (км/ч).")]
        public float maxSpeed = 70f;
        [Tooltip("Максимальная скорость движения назад (км/ч).")]
        public float maxReverseSpeed = 30f;
        [Tooltip("Сила ускорения (изменение км/ч за секунду).")]
        public float accelerationForce = 30f;
        [Tooltip("Сила естественного замедления (изменение км/ч за секунду) при отсутствии газа/тормоза.")]
        public float decelerationForce = 20f;
        [Tooltip("Сила торможения (изменение км/ч за секунду).")]
        public float brakeForce = 50f;

        [Header("Handling & Collision Effects")]
        [Tooltip("Коэффициент сопротивления в поворотах. Увеличивает потерю скорости при активном рулении.")]
        public float turningDragCoefficient = 0.25f;
        [Tooltip("Коэффициент ослабления влияния столкновения на скорость. 0 = столкновение не влияет на скорость (кроме полной остановки), 1 = серьезность столкновения полностью определяет потерю скорости.")]
        [Range(0.0f, 1.0f)]
        public float collisionImpactSoftening = 0.5f;

        // --- Internal State ---
        private float currentSpeedKph;
        private float currentSteerAngleRad;

        private Vector3 localOffsetToRearAxleCenter;
        private float simulationPlaneY_Pivot;

        private bool _isInContinuousCollision = false;
        private float _speedAtStartOfContinuousCollision = 0f;
        private float _targetSpeedAfterInitialImpact = 0f;

        private float flWheelRollAngleDeg, frWheelRollAngleDeg, rlWheelRollAngleDeg, rrWheelRollAngleDeg;

        private const float MinSpeedForCollisionProcessing_kph = 0.1f;
        private const float MinSpeedForTurningDrag_kph = 1.0f;
        private const float MinSteerForTurningDrag_rad = 0.0087f;
        private const float MinSeverityForPartialBlock_ratio = 0.05f;
        private const float KphToMps = 1000f / 3600f;
        private const float MpsToKph = 3600f / 1000f;
        private const float MIN_VALID_WHEEL_RADIUS = 0.01f; // Константа для проверки радиуса

        public float GetFLWheelRollDeg() => flWheelRollAngleDeg;
        public float GetFRWheelRollDeg() => frWheelRollAngleDeg;
        public float GetRLWheelRollDeg() => rlWheelRollAngleDeg;
        public float GetRRWheelRollDeg() => rrWheelRollAngleDeg;
        public float GetVisualSteerAngleDeg() => currentSteerAngleRad * Mathf.Rad2Deg;
        public float GetCurrentSpeedKph() => currentSpeedKph;

        void Awake()
        {
            CalculateAxleParameters();
            simulationPlaneY_Pivot = transform.position.y;

            // Проверка радиусов при старте (опционально, но полезно)
            if (frontWheelRadius < MIN_VALID_WHEEL_RADIUS)
                Debug.LogWarning($"[{nameof(AdvancedCarController)}] FrontWheelRadius ({frontWheelRadius}) слишком мал. Визуализация вращения передних колес может быть некорректной.", this);
            if (rearWheelRadius < MIN_VALID_WHEEL_RADIUS)
                Debug.LogWarning($"[{nameof(AdvancedCarController)}] RearWheelRadius ({rearWheelRadius}) слишком мал. Визуализация вращения задних колес может быть некорректной.", this);
        }

        private void CalculateAxleParameters()
        {
            if (rearLeftWheel != null && rearRightWheel != null)
            {
                Vector3 rearAxleWorldCenter = (this.rearLeftWheel.position + this.rearRightWheel.position) * 0.5f;
                localOffsetToRearAxleCenter = transform.InverseTransformPoint(rearAxleWorldCenter);

                if (frontLeftWheel != null && frontRightWheel != null)
                {
                    Vector3 frontAxleWorldCenter = (this.frontLeftWheel.position + this.frontRightWheel.position) * 0.5f;
                    Vector3 axleConnectionVector = frontAxleWorldCenter - rearAxleWorldCenter;
                    float calculatedWheelbase = Vector3.Project(axleConnectionVector, transform.forward).magnitude;

                    if (calculatedWheelbase > 0.1f)
                    {
                        this.wheelBase = calculatedWheelbase;
                    }
                    else if (this.wheelBase <= 0.1f)
                    {
                        Debug.LogError($"[{nameof(AdvancedCarController)}] Calculated wheelbase is too small and no valid 'wheelBase' set in inspector. Defaulting to 2.5m.", this);
                        this.wheelBase = 2.5f;
                    }
                }
                else if (this.wheelBase <= 0.1f)
                {
                    Debug.LogError($"[{nameof(AdvancedCarController)}] Front wheels not assigned and 'wheelBase' not set or too small. Defaulting wheelbase to 2.5m.", this);
                    this.wheelBase = 2.5f;
                }
            }
            else
            {
                Debug.LogError($"[{nameof(AdvancedCarController)}] Rear wheels (rearLeftWheel, rearRightWheel) must be assigned for correct operation! Assuming pivot is rear axle and using inspector 'wheelBase'.", this);
                localOffsetToRearAxleCenter = Vector3.zero;
                if (this.wheelBase <= 0.1f)
                {
                    Debug.LogError($"[{nameof(AdvancedCarController)}] 'wheelBase' not set or too small. Defaulting wheelbase to 2.5m.", this);
                    this.wheelBase = 2.5f;
                }
            }
        }

        public void UpdateDynamics(float dt, float steeringInputNormalized, float throttleInputNormalized)
        {
            float targetSteerAngleDeg = steeringInputNormalized * maxSteerAngle;
            float currentSteerAngleDeg = currentSteerAngleRad * Mathf.Rad2Deg;
            currentSteerAngleDeg = Mathf.MoveTowards(currentSteerAngleDeg, targetSteerAngleDeg, steerSpeed * dt);
            currentSteerAngleRad = currentSteerAngleDeg * Mathf.Deg2Rad;

            float targetSpeedKph = throttleInputNormalized * (throttleInputNormalized >= 0 ? maxSpeed : maxReverseSpeed);

            if (Mathf.Abs(throttleInputNormalized) > 0.01f)
            {
                bool isAcceleratingInCurrentDirection = (Mathf.Sign(throttleInputNormalized) == Mathf.Sign(currentSpeedKph) && currentSpeedKph != 0);
                bool isStartingFromNearStop = Mathf.Abs(currentSpeedKph) < 1.0f;

                if (isAcceleratingInCurrentDirection || isStartingFromNearStop)
                {
                    currentSpeedKph = Mathf.MoveTowards(currentSpeedKph, targetSpeedKph, accelerationForce * dt);
                }
                else
                {
                    currentSpeedKph = Mathf.MoveTowards(currentSpeedKph, targetSpeedKph, brakeForce * dt);
                }
            }
            else
            {
                currentSpeedKph = Mathf.MoveTowards(currentSpeedKph, 0, decelerationForce * dt);
            }
            currentSpeedKph = Mathf.Clamp(currentSpeedKph, -maxReverseSpeed, maxSpeed);

            ApplyTurningDragInternal(dt);
        }

        private void ApplyTurningDragInternal(float dt)
        {
            if (turningDragCoefficient <= 0 || Mathf.Abs(currentSpeedKph) < MinSpeedForTurningDrag_kph || Mathf.Abs(currentSteerAngleRad) < MinSteerForTurningDrag_rad)
                return;

            float speed_m_s = currentSpeedKph * KphToMps;
            float angularVelocityRad_carBody = 0f;

            if (Mathf.Abs(wheelBase) > 0.001f && Mathf.Abs(speed_m_s) > 0.01f)
            {
                angularVelocityRad_carBody = (speed_m_s / wheelBase) * Mathf.Tan(currentSteerAngleRad);
            }

            float lateralForceIndicator = Mathf.Abs(speed_m_s * angularVelocityRad_carBody);
            float dragDecelerationRate_mps2 = lateralForceIndicator * turningDragCoefficient;
            float speedReductionKph = dragDecelerationRate_mps2 * MpsToKph * dt;
            currentSpeedKph = Mathf.MoveTowards(currentSpeedKph, 0f, speedReductionKph);
        }

        public (Vector3 futurePosition, Quaternion futureRotation) CalculateFuturePose(float dt)
        {
            Vector3 currentPivotPos = transform.position;
            Quaternion currentPivotRot = transform.rotation;
            Vector3 currentRearAxleCenter_World = currentPivotPos + currentPivotRot * localOffsetToRearAxleCenter;
            float rearAxleTargetY = simulationPlaneY_Pivot + localOffsetToRearAxleCenter.y;
            float speed_m_s = currentSpeedKph * KphToMps;
            float angularVelocityRad_carBody = 0f;

            if (Mathf.Abs(wheelBase) > 0.001f && Mathf.Abs(speed_m_s) > 0.01f)
            {
                angularVelocityRad_carBody = (speed_m_s / wheelBase) * Mathf.Tan(currentSteerAngleRad);
            }

            float deltaAngleRadCarBody = angularVelocityRad_carBody * dt;
            Quaternion deltaRotationForPivot = Quaternion.Euler(0, deltaAngleRadCarBody * Mathf.Rad2Deg, 0);
            Quaternion targetPivotTotalRotation_World = currentPivotRot * deltaRotationForPivot;
            Quaternion halfwayRotationForDelta = currentPivotRot * Quaternion.SlerpUnclamped(Quaternion.identity, deltaRotationForPivot, 0.5f);
            Vector3 forwardForAxleMovement = halfwayRotationForDelta * Vector3.forward;
            Vector3 desiredRearAxleDisplacement = new Vector3(forwardForAxleMovement.x, 0, forwardForAxleMovement.z).normalized * speed_m_s * dt;
            if (Mathf.Approximately(speed_m_s, 0f)) desiredRearAxleDisplacement = Vector3.zero;

            Vector3 targetRearAxlePos_World = new Vector3(currentRearAxleCenter_World.x, rearAxleTargetY, currentRearAxleCenter_World.z) + desiredRearAxleDisplacement;
            Vector3 targetPivotPos_World = targetRearAxlePos_World - (targetPivotTotalRotation_World * localOffsetToRearAxleCenter);
            targetPivotPos_World.y = simulationPlaneY_Pivot;

            return (targetPivotPos_World, targetPivotTotalRotation_World);
        }

        public void ApplyCollisionFeedback(MoveResult moveResult)
        {
            bool shouldProcessCollision = moveResult.CollisionOccurred && Mathf.Abs(currentSpeedKph) > MinSpeedForCollisionProcessing_kph;

            if (!shouldProcessCollision)
            {
                if (_isInContinuousCollision) _isInContinuousCollision = false;
                return;
            }

            if (moveResult.WasMovementFullyBlocked)
            {
                currentSpeedKph = 0f;
                _isInContinuousCollision = false;
                _targetSpeedAfterInitialImpact = 0f;
                return;
            }

            if (moveResult.MovementBlockSeverity > MinSeverityForPartialBlock_ratio)
            {
                float determinedSeverity = moveResult.MovementBlockSeverity;
                if (!_isInContinuousCollision)
                {
                    _isInContinuousCollision = true;
                    _speedAtStartOfContinuousCollision = currentSpeedKph;
                    float effectiveSeverity = determinedSeverity * collisionImpactSoftening;
                    _targetSpeedAfterInitialImpact = _speedAtStartOfContinuousCollision * (1.0f - effectiveSeverity);
                    currentSpeedKph = _targetSpeedAfterInitialImpact;
                }
                else
                {
                    if (_speedAtStartOfContinuousCollision > 0 && currentSpeedKph > _targetSpeedAfterInitialImpact)
                    {
                        currentSpeedKph = _targetSpeedAfterInitialImpact;
                    }
                    else if (_speedAtStartOfContinuousCollision < 0 && currentSpeedKph < _targetSpeedAfterInitialImpact)
                    {
                        currentSpeedKph = _targetSpeedAfterInitialImpact;
                    }
                }
            }
            else
            {
                if (_isInContinuousCollision) _isInContinuousCollision = false;
            }
        }

        public void ApplyPose(Vector3 newPos, Quaternion newRot)
        {
            newPos.y = simulationPlaneY_Pivot;
            transform.SetPositionAndRotation(newPos, newRot);
        }

        /// <summary>
        /// Обновляет визуальное представление колес (поворот руля и вращение).
        /// Теперь использует отдельные радиусы для передних и задних колес.
        /// </summary>
        public void UpdateWheelVisuals(float dt)
        {
            if (Mathf.Approximately(dt, 0f)) // Если время не прошло, не обновляем визуализацию по скорости
            {
                return;
            }

            float speed_m_s = currentSpeedKph * KphToMps; // currentSpeedKph уже знаковый
            float distanceMoved = speed_m_s * dt;

            // --- Вращение передних колес ---
            if (frontLeftWheel != null || frontRightWheel != null)
            {
                if (frontWheelRadius >= MIN_VALID_WHEEL_RADIUS)
                {
                    float frontRotationIncrementDeg = (distanceMoved / frontWheelRadius) * Mathf.Rad2Deg;

                    if (frontLeftWheel != null)
                    {
                        // Используем += для вращения вперед при положительной скорости, как обсуждалось для прицепа
                        flWheelRollAngleDeg = (flWheelRollAngleDeg + frontRotationIncrementDeg) % 360f;
                    }
                    if (frontRightWheel != null)
                    {
                        frWheelRollAngleDeg = (frWheelRollAngleDeg + frontRotationIncrementDeg) % 360f;
                    }
                }
                // Опционально: предупреждение, если радиус слишком мал, а колеса назначены
                // else if (frontLeftWheel != null || frontRightWheel != null)
                // {
                //     Debug.LogWarning($"[{nameof(AdvancedCarController)}] FrontWheelRadius ({frontWheelRadius}) слишком мал. Визуализация вращения передних колес может быть некорректной.", this);
                // }
            }

            // --- Вращение задних колес ---
            if (rearLeftWheel != null || rearRightWheel != null)
            {
                if (rearWheelRadius >= MIN_VALID_WHEEL_RADIUS)
                {
                    float rearRotationIncrementDeg = (distanceMoved / rearWheelRadius) * Mathf.Rad2Deg;

                    if (rearLeftWheel != null)
                    {
                        rlWheelRollAngleDeg = (rlWheelRollAngleDeg + rearRotationIncrementDeg) % 360f;
                        rearLeftWheel.localRotation = Quaternion.AngleAxis(rlWheelRollAngleDeg, Vector3.right);
                    }
                    if (rearRightWheel != null)
                    {
                        rrWheelRollAngleDeg = (rrWheelRollAngleDeg + rearRotationIncrementDeg) % 360f;
                        rearRightWheel.localRotation = Quaternion.AngleAxis(rrWheelRollAngleDeg, Vector3.right);
                    }
                }
                // Опционально: предупреждение
                // else if (rearLeftWheel != null || rearRightWheel != null)
                // {
                //    Debug.LogWarning($"[{nameof(AdvancedCarController)}] RearWheelRadius ({rearWheelRadius}) слишком мал. Визуализация вращения задних колес может быть некорректной.", this);
                // }
            }

            // --- Поворот передних колес для руления ---
            float visualSteerAngleDeg = currentSteerAngleRad * Mathf.Rad2Deg;

            // Применяем финальные вращения (поворот + крен) к передним колесам
            if (frontLeftWheel != null)
            {
                frontLeftWheel.localRotation = Quaternion.Euler(0f, visualSteerAngleDeg, 0f) * Quaternion.AngleAxis(flWheelRollAngleDeg, Vector3.right);
            }
            if (frontRightWheel != null)
            {
                frontRightWheel.localRotation = Quaternion.Euler(0f, visualSteerAngleDeg, 0f) * Quaternion.AngleAxis(frWheelRollAngleDeg, Vector3.right);
            }
        }

        /// <summary>
        /// Применяет состояние визуализации колес (углы вращения и руления), полученное от сервера.
        /// Используется на клиентах для синхронизации визуального состояния.
        /// </summary>
        /// <param name="steerAngleDeg">Угол поворота руля в градусах.</param>
        /// <param name="flRotDeg">Угол вращения переднего левого колеса в градусах.</param>
        /// <param name="frRotDeg">Угол вращения переднего правого колеса в градусах.</param>
        /// <param name="rlRotDeg">Угол вращения заднего левого колеса в градусах.</param>
        /// <param name="rrRotDeg">Угол вращения заднего правого колеса в градусах.</param>
        public void ApplyWheelVisualState(float steerAngleDeg, float flRotDeg, float frRotDeg, float rlRotDeg, float rrRotDeg)
        {
            // 1. Синхронизируем внутренние аккумуляторы углов с полученными данными.
            // Это важно для консистентности состояния на клиенте.
            this.currentSteerAngleRad = steerAngleDeg * Mathf.Deg2Rad;
            this.flWheelRollAngleDeg = flRotDeg;
            this.frWheelRollAngleDeg = frRotDeg;
            this.rlWheelRollAngleDeg = rlRotDeg;
            this.rrWheelRollAngleDeg = rrRotDeg;

            // 2. Применяем вращения к Transform'ам колес.
            // Эта логика аналогична той, что в конце серверного метода UpdateWheelVisuals.

            // Применяем финальные вращения (поворот руля + качение) к передним колесам
            if (frontLeftWheel != null)
            {
                // Сначала поворачиваем колесо по оси Y (руление), затем вращаем по оси X (качение)
                frontLeftWheel.localRotation = Quaternion.Euler(0f, steerAngleDeg, 0f) * Quaternion.AngleAxis(flRotDeg, Vector3.right);
            }
            if (frontRightWheel != null)
            {
                frontRightWheel.localRotation = Quaternion.Euler(0f, steerAngleDeg, 0f) * Quaternion.AngleAxis(frRotDeg, Vector3.right);
            }

            // Применяем вращение (качение) к задним колесам
            if (rearLeftWheel != null)
            {
                rearLeftWheel.localRotation = Quaternion.AngleAxis(rlRotDeg, Vector3.right);
            }
            if (rearRightWheel != null)
            {
                rearRightWheel.localRotation = Quaternion.AngleAxis(rrRotDeg, Vector3.right);
            }
        }

        /// <summary>
        /// Телепортирует машину в указанную точку с указанным поворотом
        /// и сбрасывает её внутреннее состояние в состояние покоя.
        /// </summary>
        /// <param name="newWorldPosition">Новая мировая позиция для машины.</param>
        /// <param name="newWorldRotation">Новый мировой поворот для машины.</param>
        public void TeleportAndResetState(Vector3 newWorldPosition, Quaternion newWorldRotation)
        {
            // 1. Сброс основного кинематического состояния
            currentSpeedKph = 0f;

            // 2. Сброс состояния, связанного с коллизиями
            _isInContinuousCollision = false;
            _speedAtStartOfContinuousCollision = 0f;
            _targetSpeedAfterInitialImpact = 0f;

            // 3. Обновление опорного уровня Y для пивота машины.
            // Это важно, чтобы машина "осела" на правильной высоте в новой точке.
            simulationPlaneY_Pivot = newWorldPosition.y;

            // 4. Применение новой позиции и поворота.
            // Существующий метод ApplyPose использует обновленное значение simulationPlaneY_Pivot.
            ApplyPose(newWorldPosition, newWorldRotation);

            // 5. Обновление визуализации колес для отражения сброшенного состояния. 
            // Включая сброс аккумуляторов углов и сброс steer поворота колес.
            ApplyWheelVisualState(0f, 0f, 0f, 0f, 0f);
        }
        
        // Добавьте этот метод внутрь класса AdvancedCarController
        #if UNITY_EDITOR
            void OnDrawGizmosSelected()
            {
                // --- Базовые точки и оси ---
                Vector3 pivotPos = transform.position;
                Quaternion pivotRot = transform.rotation;

                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(pivotPos, 0.1f); // Точка Pivot'а машины
                Handles.Label(pivotPos + Vector3.up * 0.2f, "Pivot");

                // Линия направления вперед от Pivot'а
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(pivotPos, pivotPos + pivotRot * Vector3.forward * 2f);
                // Линия направления вправо от Pivot'а
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pivotPos, pivotPos + pivotRot * Vector3.right * 1f);
                // Линия направления вверх от Pivot'а
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pivotPos, pivotPos + pivotRot * Vector3.up * 1f);


                // --- Плоскость симуляции Y для Pivot'а ---
                // Показываем, на какой высоте Y должен находиться Pivot
                Vector3 pivotOnSimPlane = new Vector3(pivotPos.x, simulationPlaneY_Pivot, pivotPos.z);
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pivotPos, pivotOnSimPlane); // Линия от текущего Pivot до его проекции на simPlaneY
                Gizmos.DrawWireCube(pivotOnSimPlane, new Vector3(1.5f, 0.01f, 2.5f)); // Небольшая плоскость для наглядности
                Handles.Label(pivotOnSimPlane + Vector3.up * 0.1f, $"Pivot Sim Y: {simulationPlaneY_Pivot:F2}");


                // --- localOffsetToRearAxleCenter и позиция задней оси ---
                // Попытка вызвать расчет параметров, если мы в редакторе и они не инициализированы (например, до первого Play)
                // Это не всегда надежно для сложных зависимостей, но может помочь для простых случаев.
                // В Play Mode эти значения уже должны быть из Awake.
                Vector3 currentLocalOffsetToRearAxle = localOffsetToRearAxleCenter;
                float currentWheelBase = wheelBase;

                if (!Application.isPlaying)
                {
                    // Для Edit Mode можно попытаться временно рассчитать, если есть колеса
                    // Это сделает Gizmos более информативными до запуска игры
                    // Но будьте осторожны, если CalculateAxleParameters имеет побочные эффекты
                    // или зависит от других еще не инициализированных данных.
                    // В данном случае, он относительно безопасен.
                    // CalculateAxleParameters(); // Раскомментируйте, если хотите авто-обновление в Edit Mode
                    // currentLocalOffsetToRearAxle = localOffsetToRearAxleCenter;
                    // currentWheelBase = wheelBase;
                }

                Vector3 rearAxleWorldPos = pivotPos + pivotRot * currentLocalOffsetToRearAxle;
                Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
                Gizmos.DrawSphere(rearAxleWorldPos, 0.08f);
                Gizmos.DrawLine(pivotPos, rearAxleWorldPos); // Линия, показывающая смещение до задней оси
                Handles.Label(rearAxleWorldPos + Vector3.up * 0.2f, "Rear Axle Center");
                Handles.Label(pivotPos + (pivotRot * currentLocalOffsetToRearAxle * 0.5f), $"Offset: {currentLocalOffsetToRearAxle}");


                // --- Колесная база и позиция передней оси ---
                if (currentWheelBase >= 0.1f) // Используем MIN_VALID_TRAILER_LENGTH или аналогичную константу
                {
                    Vector3 frontAxleWorldPos = rearAxleWorldPos + pivotRot * Vector3.forward * currentWheelBase;
                    Gizmos.color = Color.Lerp(Color.blue, Color.white, 0.5f); // Light Blue
                    Gizmos.DrawSphere(frontAxleWorldPos, 0.08f);
                    Gizmos.DrawLine(rearAxleWorldPos, frontAxleWorldPos); // Линия колесной базы
                    Handles.Label(frontAxleWorldPos + Vector3.up * 0.2f, "Front Axle Center");
                    Handles.Label(rearAxleWorldPos + pivotRot * Vector3.forward * currentWheelBase * 0.5f, $"Wheelbase: {currentWheelBase:F2}m");
                }


                // --- Визуализация колес (радиусы и позиции) ---
                DrawWheelGizmo(frontLeftWheel, frontWheelRadius, "FL Wheel", pivotRot);
                DrawWheelGizmo(frontRightWheel, frontWheelRadius, "FR Wheel", pivotRot);
                DrawWheelGizmo(rearLeftWheel, rearWheelRadius, "RL Wheel", pivotRot);
                DrawWheelGizmo(rearRightWheel, rearWheelRadius, "RR Wheel", pivotRot);

                // --- Отображение текущей скорости и угла руля (только в Play Mode) ---
                if (Application.isPlaying)
                {
                    GUIStyle labelStyle = new GUIStyle();
                    labelStyle.normal.textColor = Color.white;
                    labelStyle.fontSize = 12;
                    // Смещаем текст немного выше машины для лучшей читаемости
                    Vector3 textPos = pivotPos + pivotRot * Vector3.up * 2.0f; 
                    Handles.Label(textPos,
                        $"Speed: {currentSpeedKph:F1} kph\n" +
                        $"Steer Angle: {currentSteerAngleRad * Mathf.Rad2Deg:F1}°",
                        labelStyle);
                }
            }

            // Вспомогательный метод для отрисовки гизмо колеса
            private void DrawWheelGizmo(Transform wheelTransform, float radius, string label, Quaternion vehicleRotation)
            {
                if (wheelTransform == null) return;
                if (radius < MIN_VALID_WHEEL_RADIUS) // Убедитесь, что MIN_VALID_WHEEL_RADIUS определена как const float в классе
                {
                    // Можно нарисовать маленькую красную точку, если радиус некорректен, но колесо назначено
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(wheelTransform.position, 0.05f);
                    Handles.Label(wheelTransform.position + Vector3.up * 0.1f, $"{label}\nRADIUS INVALID");
                    return;
                }

                Vector3 wheelPos = wheelTransform.position;
                // Ориентация колеса для отрисовки диска. Предполагаем, что колесо вращается вокруг своей локальной оси X (или transform.right)
                // и "смотрит" своей локальной осью Y вверх (или transform.up).
                Quaternion wheelVisualRotation = wheelTransform.rotation * Quaternion.Euler(0, 90, 0); // Поворачиваем так, чтобы диск был "стоячим"
                // Если колеса уже правильно ориентированы в префабе (например, их локальный Z вперед, Y вверх), то Quaternion.identity * wheelTransform.rotation

                Handles.color = Gizmos.color = new Color(0.8f, 0.8f, 0.8f, 0.7f); // Полупрозрачный серый для диска

                // Рисуем диск, представляющий колесо
                // Handles.DrawWireDisc(wheelPos, wheelTransform.right, radius); // Ось вращения - локальное право колеса
                // Gizmos не имеет DrawWireDisc, поэтому эмулируем или используем сферу
                Gizmos.DrawWireSphere(wheelPos, radius);


                // Линия, показывающая текущий "верх" колеса для индикации вращения (если нужно)
                Gizmos.color = Color.white;
                Gizmos.DrawLine(wheelPos, wheelPos + wheelTransform.up * radius);

                Handles.Label(wheelPos + transform.up * (radius * 1.1f + 0.1f), $"{label}\nR: {radius:F2}");
            }
        #endif // UNITY_EDITOR
    }
}