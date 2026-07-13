using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Реализация для 6-колесной техники (BTR-style).
    /// Кинематика:
    /// - Поворот осуществляется вокруг СРЕДНЕЙ оси (Middle Axle).
    /// - Передние колеса поворачиваются в сторону поворота.
    /// - Задние колеса поворачиваются в ПРОТИВОПОЛОЖНУЮ сторону (counter-steering).
    /// - Средние колеса не поворачиваются.
    /// </summary>
    public class SixWheelController : MonoBehaviour, ICarKinematicController
    {
        [Header("Wheel References (Visuals)")]
        public Transform frontLeftWheel;
        public Transform frontRightWheel;
        public Transform middleLeftWheel;
        public Transform middleRightWheel;
        public Transform rearLeftWheel;
        public Transform rearRightWheel;

        [Header("Wheel Radii (for Visuals)")]
        public float frontWheelRadius = 0.35f;
        public float middleWheelRadius = 0.35f;
        public float rearWheelRadius = 0.35f;

        [Header("Steering & Geometry")]
        [Tooltip("Максимальный угол поворота ПЕРЕДНИХ колес. Задние рассчитываются автоматически.")]
        public float maxSteerAngle = 35f;
        [Tooltip("Скорость руления (градусы в секунду).")]
        public float steerSpeed = 120f;
        
        // Эти параметры теперь рассчитываются автоматически, но выведены для отладки
        [Header("Calculated Geometry (Read-Only)")]
        [SerializeField, Tooltip("Расстояние от средней оси до передней.")]
        private float distToFrontAxle = 1.0f;
        [SerializeField, Tooltip("Расстояние от средней оси до задней.")]
        private float distToRearAxle = 1.0f;

        [Header("Speed & Acceleration")]
        public float maxSpeed = 70f;
        public float maxReverseSpeed = 30f;
        public float accelerationForce = 30f;
        public float decelerationForce = 20f;
        public float brakeForce = 60f;

        [Header("Handling & Collision Effects")]
        [Tooltip("Коэффициент сопротивления в поворотах.")]
        public float turningDragCoefficient = 0.25f;
        [Range(0.0f, 1.0f)]
        public float collisionImpactSoftening = 0.5f;

        // --- Internal State ---
        private float currentSpeedKph;
        private float currentSteerAngleRad;

        // Критически важно для правильного движения, если Pivot модели не идеально в центре
        private Vector3 localOffsetToMidAxleCenter; 
        private float simulationPlaneY_Pivot;

        // Visual roll angles
        private float flWheelRollDeg, frWheelRollDeg, mlWheelRollDeg, mrWheelRollDeg, rlWheelRollDeg, rrWheelRollDeg;

        // Collision State
        private bool _isInContinuousCollision = false;
        private float _speedAtStartOfContinuousCollision = 0f;
        private float _targetSpeedAfterInitialImpact = 0f;

        // Constants
        private const float MinSpeedForCollisionProcessing_kph = 0.1f;
        private const float MinSeverityForPartialBlock_ratio = 0.05f;
        private const float MinSpeedForTurningDrag_kph = 1.0f;
        private const float MinSteerForTurningDrag_rad = 0.0087f;
        private const float KphToMps = 1000f / 3600f;
        private const float MpsToKph = 3600f / 1000f;
        private const float MIN_VALID_WHEEL_RADIUS = 0.01f;

        // --- Interface Getters ---
        public float GetFLWheelRollDeg() => flWheelRollDeg;
        public float GetFRWheelRollDeg() => frWheelRollDeg;
        // Для 6 колес интерфейс может требовать расширения, но пока геттеры для совместимости
        public float GetMLWheelRollDeg() => mlWheelRollDeg; 
        public float GetMRWheelRollDeg() => mrWheelRollDeg;
        public float GetRLWheelRollDeg() => rlWheelRollDeg;
        public float GetRRWheelRollDeg() => rrWheelRollDeg;
        
        public float GetVisualSteerAngleDeg() => currentSteerAngleRad * Mathf.Rad2Deg;
        public float GetCurrentSpeedKph() => currentSpeedKph;


        void Awake()
        {
            CalculateAxleParameters();
            simulationPlaneY_Pivot = transform.position.y;
        }

        private void CalculateAxleParameters()
        {
            if (middleLeftWheel != null && middleRightWheel != null)
            {
                // 1. Находим центр средней оси (это наш центр вращения)
                Vector3 midAxleWorldCenter = (middleLeftWheel.position + middleRightWheel.position) * 0.5f;
                
                // 2. Считаем смещение от Pivot'а объекта до реального центра средней оси
                localOffsetToMidAxleCenter = transform.InverseTransformPoint(midAxleWorldCenter);

                // 3. Рассчитываем плечи рычагов (до передней и задней оси)
                if (frontLeftWheel != null)
                {
                    Vector3 frontAxleCenter = (frontLeftWheel.position + frontRightWheel.position) * 0.5f;
                    // Проецируем расстояние на ось Z машины (local forward)
                    distToFrontAxle = Mathf.Abs(transform.InverseTransformPoint(frontAxleCenter).z - localOffsetToMidAxleCenter.z);
                }
                
                if (rearLeftWheel != null)
                {
                    Vector3 rearAxleCenter = (rearLeftWheel.position + rearRightWheel.position) * 0.5f;
                    distToRearAxle = Mathf.Abs(transform.InverseTransformPoint(rearAxleCenter).z - localOffsetToMidAxleCenter.z);
                }
            }
            else
            {
                Debug.LogError($"[{nameof(SixWheelController)}] Middle wheels are required to calculate geometry!", this);
                localOffsetToMidAxleCenter = Vector3.zero;
            }
        }

        public void UpdateDynamics(float dt, float steeringInputNormalized, float throttleInputNormalized)
        {
            // 1. Руление (Input -> Deg -> Rad)
            float targetSteerAngleDeg = steeringInputNormalized * maxSteerAngle;
            float currentSteerAngleDeg = currentSteerAngleRad * Mathf.Rad2Deg;
            currentSteerAngleDeg = Mathf.MoveTowards(currentSteerAngleDeg, targetSteerAngleDeg, steerSpeed * dt);
            currentSteerAngleRad = currentSteerAngleDeg * Mathf.Deg2Rad;

            // 2. Скорость
            float targetSpeedKph = throttleInputNormalized * (throttleInputNormalized >= 0 ? maxSpeed : maxReverseSpeed);

            if (Mathf.Abs(throttleInputNormalized) > 0.01f)
            {
                bool isAcceleratingInCurrentDirection = (Mathf.Sign(throttleInputNormalized) == Mathf.Sign(currentSpeedKph) && Mathf.Abs(currentSpeedKph) > 0.1f);
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
                currentSpeedKph = Mathf.MoveTowards(currentSpeedKph, 0f, decelerationForce * dt);
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

            // Базовый расчет угловой скорости (через переднюю ось)
            if (distToFrontAxle > 0.001f && Mathf.Abs(speed_m_s) > 0.01f)
            {
                angularVelocityRad_carBody = (speed_m_s / distToFrontAxle) * Mathf.Tan(currentSteerAngleRad);
            }

            // --- ИСПРАВЛЕНИЕ: Множитель "Двухстороннего руления" ---
            // В тракторе (AdvancedCarController) поворачивает только перед.
            // Здесь поворачивают ОБЕ оси. Задняя ось тоже совершает работу по изменению вектора движения.
            // Мы добавляем коэффициент, пропорциональный длине задней части.
            // Если задняя часть такая же длинная, как передняя, сопротивление будет (1 + 1) = 2x.
            
            float geometryDragMultiplier = 1.0f;
            if (distToFrontAxle > 0.001f)
            {
                geometryDragMultiplier = 1.0f + (distToRearAxle / distToFrontAxle);
            }

            // Для твоих данных: 1.0 + (1.59 / 2.26) ≈ 1.7
            // То есть торможение станет в 1.7 раза сильнее при тех же настройках.

            float lateralForceIndicator = Mathf.Abs(speed_m_s * angularVelocityRad_carBody);
            
            // Применяем мультипликатор к силе сопротивления
            float dragDecelerationRate = lateralForceIndicator * turningDragCoefficient * geometryDragMultiplier;
            
            float speedReductionKph = dragDecelerationRate * MpsToKph * dt;
            
            currentSpeedKph = Mathf.MoveTowards(currentSpeedKph, 0f, speedReductionKph);
        }

        public (Vector3 futurePosition, Quaternion futureRotation) CalculateFuturePose(float dt)
        {            
            Vector3 currentPivotPos = transform.position;
            Quaternion currentPivotRot = transform.rotation;
            
            // 1. Находим, где сейчас средняя ось в мире (аналог задней оси у 4-колесной)
            Vector3 currentMidAxleCenter_World = currentPivotPos + currentPivotRot * localOffsetToMidAxleCenter;
            float midAxleTargetY = simulationPlaneY_Pivot + localOffsetToMidAxleCenter.y;
            
            float speed_m_s = currentSpeedKph * KphToMps;
            float angularVelocityRad_carBody = 0f;

            // 2. Считаем угловую скорость поворота корпуса
            if (distToFrontAxle > 0.001f && Mathf.Abs(speed_m_s) > 0.01f)
            {
                angularVelocityRad_carBody = (speed_m_s / distToFrontAxle) * Mathf.Tan(currentSteerAngleRad);
            }

            // 3. Интегрируем поворот
            float deltaAngleRadCarBody = angularVelocityRad_carBody * dt;
            Quaternion deltaRotationForPivot = Quaternion.Euler(0, deltaAngleRadCarBody * Mathf.Rad2Deg, 0);
            Quaternion targetPivotTotalRotation_World = currentPivotRot * deltaRotationForPivot;

            // 4. Интегрируем смещение (вдоль направления, которое было "посередине" поворота, для большей точности дуги)
            Quaternion halfwayRotationForDelta = currentPivotRot * Quaternion.SlerpUnclamped(Quaternion.identity, deltaRotationForPivot, 0.5f);
            Vector3 forwardForAxleMovement = halfwayRotationForDelta * Vector3.forward;
            
            Vector3 desiredMidAxleDisplacement = new Vector3(forwardForAxleMovement.x, 0, forwardForAxleMovement.z).normalized * speed_m_s * dt;
            if (Mathf.Approximately(speed_m_s, 0f)) desiredMidAxleDisplacement = Vector3.zero;

            // 5. Собираем новую позицию Pivot'а обратно из новой позиции оси
            Vector3 targetMidAxlePos_World = new Vector3(currentMidAxleCenter_World.x, midAxleTargetY, currentMidAxleCenter_World.z) + desiredMidAxleDisplacement;
            Vector3 targetPivotPos_World = targetMidAxlePos_World - (targetPivotTotalRotation_World * localOffsetToMidAxleCenter);
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

        public void UpdateWheelVisuals(float dt)
        {
            if (Mathf.Approximately(dt, 0f)) return;

            float speed_m_s = currentSpeedKph * KphToMps;
            float distanceMoved = speed_m_s * dt;

            // Хелпер для расчета вращения (Roll)
            void UpdateRoll(ref float rollAngle, float radius)
            {
                if (radius >= MIN_VALID_WHEEL_RADIUS)
                {
                    float rotationIncrementDeg = (distanceMoved / radius) * Mathf.Rad2Deg;
                    rollAngle = (rollAngle + rotationIncrementDeg) % 360f;
                }
            }

            UpdateRoll(ref flWheelRollDeg, frontWheelRadius);
            UpdateRoll(ref frWheelRollDeg, frontWheelRadius);
            UpdateRoll(ref mlWheelRollDeg, middleWheelRadius);
            UpdateRoll(ref mrWheelRollDeg, middleWheelRadius);
            UpdateRoll(ref rlWheelRollDeg, rearWheelRadius);
            UpdateRoll(ref rrWheelRollDeg, rearWheelRadius);

            // 1. Передние колеса (Рулят нормально)
            float frontSteerDeg = currentSteerAngleRad * Mathf.Rad2Deg;
            ApplyWheelTransform(frontLeftWheel, frontSteerDeg, flWheelRollDeg);
            ApplyWheelTransform(frontRightWheel, frontSteerDeg, frWheelRollDeg);

            // 2. Средние колеса (Не рулят)
            ApplyWheelTransform(middleLeftWheel, 0f, mlWheelRollDeg);
            ApplyWheelTransform(middleRightWheel, 0f, mrWheelRollDeg);

            // 3. Задние колеса (Рулят противофазно)
            float rearSteerDeg = ComputeRearSteerFromFrontRad(currentSteerAngleRad);
            ApplyWheelTransform(rearLeftWheel, rearSteerDeg, rlWheelRollDeg);
            ApplyWheelTransform(rearRightWheel, rearSteerDeg, rrWheelRollDeg);
        }

        private float ComputeRearSteerFromFrontRad(float frontRad)
        {
            // Геометрический расчет для counter-steering
            // Если мы поворачиваем вокруг средней оси, то задние колеса должны смотреть "наружу" круга так же, как передние "внутрь"
            // tan(rear) = (L_rear / L_front) * tan(front)
            if (Mathf.Abs(frontRad) < 1e-4f) return 0f;
            if (distToFrontAxle < 0.001f) return 0f; // Защита от деления на 0

            float tanFront = Mathf.Tan(frontRad);
            float tanRear = (distToRearAxle / distToFrontAxle) * tanFront;
            
            // Минус, так как поворот в другую сторону
            return -Mathf.Atan(tanRear) * Mathf.Rad2Deg;
        }

        private void ApplyWheelTransform(Transform wheel, float steerDeg, float rollDeg)
        {
            if (wheel != null)
            {
                wheel.localRotation = Quaternion.Euler(0f, steerDeg, 0f) * Quaternion.AngleAxis(rollDeg, Vector3.right);
            }
        }

        public void ApplyWheelVisualState(float steerDeg, float fl, float fr, float rl, float rr)
        {
            // Заглушка
        }

        public void TeleportAndResetState(Vector3 newWorldPosition, Quaternion newWorldRotation)
        {
            // Полная копия логики сброса из AdvancedCarController
            currentSpeedKph = 0f;
            currentSteerAngleRad = 0f;
            
            _isInContinuousCollision = false;
            _speedAtStartOfContinuousCollision = 0f;
            _targetSpeedAfterInitialImpact = 0f;

            simulationPlaneY_Pivot = newWorldPosition.y;

            ApplyPose(newWorldPosition, newWorldRotation);

            flWheelRollDeg = frWheelRollDeg = mlWheelRollDeg = mrWheelRollDeg = rlWheelRollDeg = rrWheelRollDeg = 0f;
            
            // Сбрасываем визуал колес в 0
            UpdateWheelVisuals(0f); // 0 time delta just ensures rotations are applied
            // Но так как delta 0, rotationIncrement будет 0. Нам нужно принудительно выставить Transforms:
            ApplyWheelTransform(frontLeftWheel, 0, 0);
            ApplyWheelTransform(frontRightWheel, 0, 0);
            ApplyWheelTransform(middleLeftWheel, 0, 0);
            ApplyWheelTransform(middleRightWheel, 0, 0);
            ApplyWheelTransform(rearLeftWheel, 0, 0);
            ApplyWheelTransform(rearRightWheel, 0, 0);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // --- Базовые точки ---
            Vector3 pivotPos = transform.position;
            Quaternion pivotRot = transform.rotation;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pivotPos, 0.1f);
            Handles.Label(pivotPos + Vector3.up * 0.2f, "Pivot");
            
            // --- Средняя ось (Центр вращения) ---
            // Если в Edit mode смещение еще не рассчитано, попробуем рассчитать (аккуратно)
            Vector3 offset = localOffsetToMidAxleCenter;
            if (!Application.isPlaying && middleLeftWheel != null && middleRightWheel != null)
            {
                 Vector3 midAxleWorld = (middleLeftWheel.position + middleRightWheel.position) * 0.5f;
                 offset = transform.InverseTransformPoint(midAxleWorld);
            }

            Vector3 midAxlePos = pivotPos + pivotRot * offset;
            Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
            Gizmos.DrawSphere(midAxlePos, 0.12f);
            Gizmos.DrawLine(pivotPos, midAxlePos);
            Handles.Label(midAxlePos + Vector3.up * 0.2f, "Middle Axle (Center of Rotation)");

            // --- Передняя ось ---
            Vector3 fPos = midAxlePos + pivotRot * Vector3.forward * distToFrontAxle;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(midAxlePos, fPos);
            Gizmos.DrawSphere(fPos, 0.08f);
            Handles.Label(fPos, $"L_Front: {distToFrontAxle:F2}");

            // --- Задняя ось ---
            Vector3 rPos = midAxlePos - pivotRot * Vector3.forward * distToRearAxle;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(midAxlePos, rPos);
            Gizmos.DrawSphere(rPos, 0.08f);
            Handles.Label(rPos, $"L_Rear: {distToRearAxle:F2}");

            // --- Колеса ---
            DrawWheelGizmo(frontLeftWheel, frontWheelRadius, "FL", pivotRot);
            DrawWheelGizmo(frontRightWheel, frontWheelRadius, "FR", pivotRot);
            DrawWheelGizmo(middleLeftWheel, middleWheelRadius, "ML", pivotRot);
            DrawWheelGizmo(middleRightWheel, middleWheelRadius, "MR", pivotRot);
            DrawWheelGizmo(rearLeftWheel, rearWheelRadius, "RL", pivotRot);
            DrawWheelGizmo(rearRightWheel, rearWheelRadius, "RR", pivotRot);
            
            // --- ICR (Instant Center of Rotation) визуализация ---
            if (Mathf.Abs(currentSteerAngleRad) > 0.05f)
            {
                float R = distToFrontAxle / Mathf.Tan(currentSteerAngleRad);
                Vector3 icrPos = midAxlePos + pivotRot * Vector3.right * R;
                Gizmos.color = new Color(1, 0, 1, 0.5f);
                Gizmos.DrawWireSphere(icrPos, 0.5f);
                Gizmos.DrawLine(midAxlePos, icrPos);
                Gizmos.DrawLine(fPos, icrPos);
            }

            if (Application.isPlaying)
            {
                Vector3 textPos = pivotPos + pivotRot * Vector3.up * 2.0f;
                Handles.Label(textPos, $"Speed: {currentSpeedKph:F1} kph\nSteer: {currentSteerAngleRad * Mathf.Rad2Deg:F1}°");
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
#endif
    }
}