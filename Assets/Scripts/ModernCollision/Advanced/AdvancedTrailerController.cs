using UnityEngine;
using UnityEditor;

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Расширенная версия TrailerController с автоматическим расчетом длины прицепа
    /// и визуализацией вращения колес.
    /// Основная логика движения (ComputeFuturePose, ApplyPose) идентична TrailerController.
    /// </summary>
    public class AdvancedTrailerController : MonoBehaviour
    {
        [Header("Hitch Points (Required)")]
        [Tooltip("Ссылка на Transform точки сцепки на ведущей машине (внешний объект). Должен быть назначен.")]
        public Transform carHitch;
        [Tooltip("Ссылка на Transform точки сцепки на самом прицепе (обычно дочерний объект этого GameObject). Должен быть назначен.")]
        public Transform trailerHitch;

        [Header("Trailer Geometry")]
        [Tooltip("Длина прицепа (эффективное расстояние от его точки сцепки до оси). Будет рассчитана автоматически, если заданы 'trailerHitch' и задние колеса. Иначе используется значение из инспектора.")]
        public float trailerLength = 3f;

        [Header("Angle Clamping (Optional)")]
        [Tooltip("Использовать ли ограничение угла между машиной и прицепом для предотвращения \"складывания\".")]
        public bool useAngleClamp = false;
        [Tooltip("Максимальный угол \"складывания\" прицепа относительно машины (в градусах). Значение от 0 до 180.")]
        [Range(0f, 180f)]
        public float maxFoldAngle = 80f;

        [Header("Wheel Setup (Optional - for visuals & auto length calc)")]
        [Tooltip("Transform левого заднего колеса прицепа. Используется для визуализации и авто-расчета длины.")]
        public Transform rearLeftWheel_Trailer;
        [Tooltip("Transform правого заднего колеса прицепа. Используется для визуализации и авто-расчета длины.")]
        public Transform rearRightWheel_Trailer;
        [Tooltip("Радиус колеса прицепа. Используется для корректной визуализации вращения.")]
        public float wheelRadius_Trailer = 0.35f;

        // Внутреннее состояние (как в оригинальном TrailerController)
        private float _trailerAngleDeg;
        private Vector3 _prevRootPos;
        private Quaternion _prevRootRot;
        private Vector3 _lastCarHitchPos;

        // Состояние для визуализации колес
        private float tlWheelRollAngleDeg = 0f;
        private float trWheelRollAngleDeg = 0f;

        private const float MIN_VALID_TRAILER_LENGTH = 0.1f;
        private const float DEFAULT_TRAILER_LENGTH = 3.0f;
        private const float MIN_VALID_WHEEL_RADIUS = 0.01f;
        private const float DEFAULT_WHEEL_RADIUS = 0.35f;
        private const float EPSILON = 1e-6f; // Малое значение для сравнения с нулем

        public float GetTLWheelRollDeg() => tlWheelRollAngleDeg;
        public float GetTRWheelRollDeg() => trWheelRollAngleDeg;

        void Awake()
        {
            if (carHitch == null)
            {
                Debug.LogError($"[{nameof(AdvancedTrailerController)}] 'carHitch' не назначен!", this);
                enabled = false; return;
            }
            if (trailerHitch == null)
            {
                Debug.LogError($"[{nameof(AdvancedTrailerController)}] 'trailerHitch' не назначен!", this);
                enabled = false; return;
            }

            CalculateTrailerLengthInternal();

            if (wheelRadius_Trailer < MIN_VALID_WHEEL_RADIUS)
            {
                if (rearLeftWheel_Trailer != null || rearRightWheel_Trailer != null)
                {
                    Debug.LogWarning($"[{nameof(AdvancedTrailerController)}] 'wheelRadius_Trailer' ({wheelRadius_Trailer}) некорректен. Используется по умолчанию: {DEFAULT_WHEEL_RADIUS}м.", this);
                    wheelRadius_Trailer = DEFAULT_WHEEL_RADIUS;
                }
            }

            _prevRootPos = transform.position;
            _prevRootRot = transform.rotation;
            _trailerAngleDeg = transform.eulerAngles.y;
            _lastCarHitchPos = carHitch.position;
        }

        private void CalculateTrailerLengthInternal()
        {
            bool canCalculateLength = trailerHitch != null && rearLeftWheel_Trailer != null && rearRightWheel_Trailer != null;
            if (canCalculateLength)
            {
                Vector3 trailerAxleWorldCenter = (rearLeftWheel_Trailer.position + rearRightWheel_Trailer.position) * 0.5f;
                Vector3 trailerHitchWorldPos = trailerHitch.position;
                Vector3 axleToHitchWorldVec = trailerHitchWorldPos - trailerAxleWorldCenter;
                Vector3 axleToHitchLocalVec = transform.InverseTransformDirection(axleToHitchWorldVec);
                float calculatedLength = Mathf.Abs(axleToHitchLocalVec.z);

                if (calculatedLength >= MIN_VALID_TRAILER_LENGTH)
                {
                    if (trailerLength >= MIN_VALID_TRAILER_LENGTH && Mathf.Abs(trailerLength - calculatedLength) > 0.1f)
                    {
                        Debug.Log($"[{nameof(AdvancedTrailerController)}] 'trailerLength' ({trailerLength}м) из инспектора отличается от рассчитанного ({calculatedLength}м). Используется рассчитанное.", this);
                    }
                    trailerLength = calculatedLength;
                }
                else
                {
                    if (trailerLength < MIN_VALID_TRAILER_LENGTH)
                    {
                        Debug.LogError($"[{nameof(AdvancedTrailerController)}] Рассчитанная 'trailerLength' ({calculatedLength}м) и значение в инспекторе ({trailerLength}м) некорректны. Используется по умолчанию: {DEFAULT_TRAILER_LENGTH}м.", this);
                        trailerLength = DEFAULT_TRAILER_LENGTH;
                    }
                    else
                    {
                        Debug.LogWarning($"[{nameof(AdvancedTrailerController)}] Рассчитанная 'trailerLength' ({calculatedLength}м) слишком мала. Используется значение из инспектора: {trailerLength}м.", this);
                    }
                }
            }
            else
            {
                if (trailerLength < MIN_VALID_TRAILER_LENGTH)
                {
                    Debug.LogError($"[{nameof(AdvancedTrailerController)}] 'trailerLength' не может быть рассчитана (нет колес/сцепки) и значение в инспекторе ({trailerLength}м) некорректно. Используется по умолчанию: {DEFAULT_TRAILER_LENGTH}м.", this);
                    trailerLength = DEFAULT_TRAILER_LENGTH;
                }
                else
                {
                    Debug.Log($"[{nameof(AdvancedTrailerController)}] 'trailerLength' ({trailerLength}м) из инспектора, т.к. авто-расчет невозможен.", this);
                }
            }
        }

        public (Vector3 futurePos, Quaternion futureRot, float futureAngleDeg) ComputeFuturePose(
            float dt,
            Vector3 futureCarHitchWorldPos,
            float futureCarYRotationDeg)
        {
            Vector3 trailerHitchLocalOffset = trailerHitch.localPosition;
            Vector3 oldTrailerHitchWorldPos = _prevRootPos + _prevRootRot * trailerHitchLocalOffset;
            Vector3 carHitchDeltaWorldPos = (futureCarHitchWorldPos - _lastCarHitchPos);
            carHitchDeltaWorldPos.y = 0f;

            float hitchSpeed_mps = 0f; // Локальная переменная для магнитуды скорости сцепки
            if (dt > EPSILON)
            {
                hitchSpeed_mps = carHitchDeltaWorldPos.magnitude / dt;
            }

            float newTrailerAngleDeg = _trailerAngleDeg;
            if (hitchSpeed_mps > EPSILON && trailerLength >= MIN_VALID_TRAILER_LENGTH)
            {
                float carHitchSpeedDirectionDeg = Mathf.Atan2(carHitchDeltaWorldPos.x, carHitchDeltaWorldPos.z) * Mathf.Rad2Deg;
                float phi_minus_Psi_rad = Mathf.Deg2Rad * (carHitchSpeedDirectionDeg - _trailerAngleDeg);
                float dPsi_rad_per_sec = (hitchSpeed_mps / trailerLength) * Mathf.Sin(phi_minus_Psi_rad);
                float deltaAngleDegThisFrame = dPsi_rad_per_sec * Mathf.Rad2Deg * dt;
                newTrailerAngleDeg = _trailerAngleDeg + deltaAngleDegThisFrame;

                if (useAngleClamp)
                {
                    float relativeAngleDeg = Mathf.DeltaAngle(futureCarYRotationDeg, newTrailerAngleDeg);
                    float clampedRelativeAngleDeg = Mathf.Clamp(relativeAngleDeg, -maxFoldAngle, maxFoldAngle);
                    newTrailerAngleDeg = futureCarYRotationDeg + clampedRelativeAngleDeg;
                }
            }

            float rotationThisFrameDeg = Mathf.DeltaAngle(_trailerAngleDeg, newTrailerAngleDeg);
            Quaternion rotationDelta = Quaternion.Euler(0f, rotationThisFrameDeg, 0f);
            Quaternion newRootRot = _prevRootRot * rotationDelta;
            Vector3 vectorFromOldHitchToOldRoot = _prevRootPos - oldTrailerHitchWorldPos;
            Vector3 vectorFromOldHitchToNewRoot_rotated = rotationDelta * vectorFromOldHitchToOldRoot;
            Vector3 newRootPos_afterPureRotation = oldTrailerHitchWorldPos + vectorFromOldHitchToNewRoot_rotated;
            Vector3 newTrailerHitchWorldPos_afterPureRotation = newRootPos_afterPureRotation + newRootRot * trailerHitchLocalOffset;
            Vector3 correctionOffset = futureCarHitchWorldPos - newTrailerHitchWorldPos_afterPureRotation;
            Vector3 finalNewRootPos = newRootPos_afterPureRotation + correctionOffset;

            return (finalNewRootPos, newRootRot, newTrailerAngleDeg);
        }

        public void ApplyPose(Vector3 newPos, Quaternion newRot, float newAngleDeg)
        {
            // Перед применением новой позы, текущая transform.position - это _prevRootPos (почти)
            // Мы сохраняем ее, чтобы потом вычислить смещение для колес.
            // Однако, _prevRootPos уже хранит позицию *до* этого шага ApplyPose, что нам и нужно.

            transform.SetPositionAndRotation(newPos, newRot);

            // Обновляем состояние для следующего кадра
            // _prevRootPos будет обновлен ПОСЛЕ того, как UpdateWheelVisuals использует его старое значение
            // _prevRootPos = newPos; // ЭТО ОБНОВЛЕНИЕ ПЕРЕНЕСЕНО В КОНЕЦ UpdateWheelVisuals или в AdvancedCarDriver

            _trailerAngleDeg = newAngleDeg; // Угол обновляется сразу
            _lastCarHitchPos = newPos + (newRot * trailerHitch.localPosition); // Обновляем позицию сцепки для следующего ComputeFuturePose
            // _prevRootRot обновляется также после UpdateWheelVisuals
        }


        public void UpdateWheelVisuals(float dt)
        {
            if (wheelRadius_Trailer < MIN_VALID_WHEEL_RADIUS || (rearLeftWheel_Trailer == null && rearRightWheel_Trailer == null))
            {
                // Обновляем _prevRootPos и _prevRootRot здесь, даже если колеса не вращаются,
                // чтобы состояние было консистентным для следующего FixedUpdate.
                // Это предполагает, что UpdateWheelVisuals вызывается *всегда* после ApplyPose.
                _prevRootPos = transform.position;
                _prevRootRot = transform.rotation;
                return;
            }
            if (Mathf.Approximately(dt, 0f)) // Если dt нулевой, смещения нет
            {
                _prevRootPos = transform.position;
                _prevRootRot = transform.rotation;
                return;
            }

            // Фактическое смещение корневого Transform'а прицепа за последний dt (в мировых координатах)
            // transform.position - это новая позиция (newPos из ApplyPose)
            // _prevRootPos - это позиция до вызова ApplyPose на этом шаге FixedUpdate
            Vector3 worldDisplacementSinceLastApplyPose = transform.position - _prevRootPos;

            // Трансформируем это смещение в локальные координаты прицепа (используя его текущую, новую ориентацию)
            // Это покажет, насколько прицеп сместился вдоль своих локальных осей X, Y, Z.
            Vector3 localDisplacement = transform.InverseTransformDirection(worldDisplacementSinceLastApplyPose);

            // localDisplacement.z - это смещение вдоль продольной оси прицепа (положительное - вперед, отрицательное - назад).
            // Это и есть "пройденное расстояние" со знаком.
            float distanceMovedAlongTrailerZ = localDisplacement.z;

            float wheelRotationAngleDegIncrement = 0;
            if (Mathf.Abs(distanceMovedAlongTrailerZ) > EPSILON) // Если было значимое смещение
            {
                wheelRotationAngleDegIncrement = (distanceMovedAlongTrailerZ / wheelRadius_Trailer) * Mathf.Rad2Deg;
            }

            // Ваша логика: tlWheelRollAngleDeg -= wheelRotationAngleDegIncrement;
            // Если distanceMovedAlongTrailerZ > 0 (вперед), increment > 0, угол уменьшается.
            // Если distanceMovedAlongTrailerZ < 0 (назад), increment < 0, угол увеличивается.
            // Это должно дать правильное направление вращения при вашей настройке.
            if (rearLeftWheel_Trailer != null)
            {
                tlWheelRollAngleDeg = (tlWheelRollAngleDeg + wheelRotationAngleDegIncrement) % 360f;
                rearLeftWheel_Trailer.localRotation = Quaternion.AngleAxis(tlWheelRollAngleDeg, Vector3.right);
            }
            if (rearRightWheel_Trailer != null)
            {
                trWheelRollAngleDeg = (trWheelRollAngleDeg + wheelRotationAngleDegIncrement) % 360f;
                rearRightWheel_Trailer.localRotation = Quaternion.AngleAxis(trWheelRollAngleDeg, Vector3.right);
            }

            // Обновляем _prevRootPos и _prevRootRot здесь, ПОСЛЕ того, как они были использованы для вычисления смещения
            _prevRootPos = transform.position;
            _prevRootRot = transform.rotation;
        }

        /// <summary>
        /// Применяет состояние вращения колес прицепа, полученное от сервера.
        /// Используется на клиентах для синхронизации визуального состояния.
        /// </summary>
        /// <param name="leftWheelRotDeg">Угол вращения левого колеса прицепа в градусах.</param>
        /// <param name="rightWheelRotDeg">Угол вращения правого колеса прицепа в градусах.</param>
        public void ApplyTrailerWheelVisualState(float leftWheelRotDeg, float rightWheelRotDeg)
        {
            // 1. Синхронизируем внутренние аккумуляторы углов
            this.tlWheelRollAngleDeg = leftWheelRotDeg;
            this.trWheelRollAngleDeg = rightWheelRotDeg;

            // 2. Применяем вращения к Transform'ам колес
            if (rearLeftWheel_Trailer != null)
            {
                rearLeftWheel_Trailer.localRotation = Quaternion.AngleAxis(leftWheelRotDeg, Vector3.right);
            }
            if (rearRightWheel_Trailer != null)
            {
                rearRightWheel_Trailer.localRotation = Quaternion.AngleAxis(rightWheelRotDeg, Vector3.right);
            }
        }

        /// <summary>
        /// Телепортирует прицеп в указанную точку с указанным поворотом
        /// и сбрасывает его внутреннее состояние, как будто сцена была перезапущена.
        /// </summary>
        /// <param name="newWorldPosition">Новая мировая позиция для прицепа.</param>
        /// <param name="newWorldRotation">Новый мировой поворот для прицепа.</param>
        public void TeleportAndResetState(Vector3 newWorldPosition, Quaternion newWorldRotation)
        {
            // 1. Применяем новую позицию и поворот к Transform'у прицепа
            transform.SetPositionAndRotation(newWorldPosition, newWorldRotation);

            // 2. Сброс внутренних переменных состояния до значений "как при запуске сцены"

            // _trailerAngleDeg в Awake инициализируется как transform.eulerAngles.y
            _trailerAngleDeg = newWorldRotation.eulerAngles.y;

            // _prevRootPos и _prevRootRot в Awake инициализируются текущей позицией/поворотом
            _prevRootPos = newWorldPosition;
            _prevRootRot = newWorldRotation;

            // _lastCarHitchPos в Awake инициализируется как carHitch.position.
            // Это важно для корректного расчета движения в следующем кадре ComputeFuturePose.
            if (carHitch != null)
            {
                _lastCarHitchPos = carHitch.position;
            }
            else
            {
                // Если carHitch не назначен (хотя Awake должен был бы отключить компонент),
                // установим _lastCarHitchPos в позицию собственной сцепки прицепа как запасной вариант.
                // Это менее идеально для немедленного начала следования.
                if (trailerHitch != null)
                {
                    _lastCarHitchPos = newWorldPosition + (newWorldRotation * trailerHitch.localPosition);
                    Debug.LogWarning($"[{nameof(AdvancedTrailerController)}] TeleportAndResetState: 'carHitch' не назначен. _lastCarHitchPos установлен по собственной сцепке прицепа.", this);
                }
                else
                {
                    // Если и trailerHitch не назначен, ситуация сложная.
                    _lastCarHitchPos = newWorldPosition; // Крайний случай
                    Debug.LogError($"[{nameof(AdvancedTrailerController)}] TeleportAndResetState: 'carHitch' и 'trailerHitch' не назначены. Состояние _lastCarHitchPos может быть некорректным.", this);
                }
            }

            // 3. Обновление визуализации колес для отражения сброшенного состояния.
            // Внутри метода также происходит сброс аккумуляторов вращения колес для визуализации
            ApplyTrailerWheelVisualState(0f, 0f);

            // 4. Если на прицепе используется Rigidbody, сбросьте его скорости
            /*
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            */

            // 5. Геометрические параметры, такие как trailerLength и wheelRadius_Trailer,
            // обычно устанавливаются или рассчитываются в Awake на основе конфигурации префаба.
            // Для простого сброса состояния их пересчитывать не обязательно, если геометрия прицепа не менялась.
            // Если бы CalculateTrailerLengthInternal() нужно было бы вызвать, это можно было бы сделать здесь.
            // CalculateTrailerLengthInternal();

            // Компонент может быть выключен, если в Awake не были найдены carHitch или trailerHitch.
            // Если телепортация должна "исправить" ситуацию (например, прицепить к новой машине),
            // возможно, потребуется его включить:
            // if (!enabled && carHitch != null && trailerHitch != null)
            // {
            //     enabled = true;
            // }
        }
        
        #if UNITY_EDITOR
            /// <summary>
            /// Отрисовывает гизмо в редакторе для визуализации ключевых параметров прицепа.
            /// </summary>
            void OnDrawGizmosSelected()
            {
                // --- 1. Рисуем колеса и колесную ось ---
                if (rearLeftWheel_Trailer != null && rearRightWheel_Trailer != null)
                {
                    Vector3 leftWheelPos = rearLeftWheel_Trailer.position;
                    Vector3 rightWheelPos = rearRightWheel_Trailer.position;
                    Vector3 axleCenter = (leftWheelPos + rightWheelPos) * 0.5f;

                    // Рисуем линию колесной оси
                    Gizmos.color = Color.gray;
                    Gizmos.DrawLine(leftWheelPos, rightWheelPos);

                    // Рисуем точку центра оси
                    Gizmos.color = Color.white;
                    Gizmos.DrawSphere(axleCenter, 0.05f);
                    Handles.Label(axleCenter + Vector3.up * 0.15f, "Axle Center");

                    // Рисуем сферы для колес, чтобы визуализировать их радиус
                    if (wheelRadius_Trailer >= MIN_VALID_WHEEL_RADIUS)
                    {
                        Gizmos.color = new Color(0.8f, 0.8f, 0.8f, 0.4f); // Полупрозрачный серый
                        Gizmos.DrawWireSphere(leftWheelPos, wheelRadius_Trailer);
                        Handles.Label(leftWheelPos + Vector3.up * (wheelRadius_Trailer + 0.1f), "Left Wheel");

                        Gizmos.DrawWireSphere(rightWheelPos, wheelRadius_Trailer);
                        Handles.Label(rightWheelPos + Vector3.up * (wheelRadius_Trailer + 0.1f), "Right Wheel");
                    }
                }

                // --- 2. Рисуем точку сцепки и линию длины прицепа ---
                if (trailerHitch != null)
                {
                    Vector3 hitchPos = trailerHitch.position;

                    // Рисуем точку сцепки
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(hitchPos, 0.08f);
                    Handles.Label(hitchPos + Vector3.up * 0.15f, "Trailer Hitch");

                    // --- 3. Рисуем представление длины прицепа ---
                    // Для этого нам нужен центр оси, который доступен только если колеса назначены
                    if (rearLeftWheel_Trailer != null && rearRightWheel_Trailer != null)
                    {
                        Vector3 axleCenter = (rearLeftWheel_Trailer.position + rearRightWheel_Trailer.position) * 0.5f;

                        // Линия от сцепки до центра оси наглядно представляет направление и связь
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(hitchPos, axleCenter);

                        // В режиме редактирования (когда игра не запущена), Awake() не вызывается,
                        // и trailerLength может быть не рассчитан. Давайте рассчитаем его "на лету"
                        // для более точного отображения в гизмо.
                        float lengthForDisplay = this.trailerLength;
                        if (!Application.isPlaying)
                        {
                            // Повторяем логику из CalculateTrailerLengthInternal для превью в редакторе
                            Vector3 axleToHitchWorldVec = hitchPos - axleCenter;
                            // Проецируем вектор от оси к сцепке на локальную ось Z (вперед) прицепа
                            float calculatedLength = Vector3.Dot(axleToHitchWorldVec, transform.forward);

                            if (calculatedLength >= MIN_VALID_TRAILER_LENGTH)
                            {
                                lengthForDisplay = calculatedLength;
                            }
                        }

                        // Отображаем значение длины прицепа
                        Vector3 midPoint = (hitchPos + axleCenter) * 0.5f;
                        GUIStyle style = new GUIStyle();
                        style.normal.textColor = Color.yellow;
                        Handles.Label(midPoint, $"Trailer Length: {lengthForDisplay:F2}m", style);
                    }
                }
            }
        #endif // Завершение блока UNITY_EDITOR
    }
}