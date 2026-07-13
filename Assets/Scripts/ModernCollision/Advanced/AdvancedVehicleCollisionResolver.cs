using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Структура для возврата результата перемещения из AdvancedVehicleCollisionResolver.
    /// </summary>
    public struct MoveResult
    {
        /// <summary>
        /// Произошло ли столкновение или депенетрация во время расчета.
        /// </summary>
        public bool CollisionOccurred;

        /// <summary>
        /// Финальная рассчитанная позиция после разрешения столкновений.
        /// </summary>
        public Vector3 FinalPosition;

        /// <summary>
        /// Финальный рассчитанный поворот после разрешения столкновений.
        /// </summary>
        public Quaternion FinalRotation;

        /// <summary>
        /// Было ли движение полностью заблокировано в плоскости XZ (т.е. объект не смог продвинуться в желаемом направлении).
        /// </summary>
        public bool WasMovementFullyBlocked;

        /// <summary>
        /// Степень блокировки движения в плоскости XZ (0 = нет блокировки, 1 = полная блокировка).
        /// Рассчитывается на основе того, какая часть желаемого смещения была достигнута в желаемом направлении.
        /// </summary>
        public float MovementBlockSeverity;
    }

    /// <summary>
    /// Отвечает за кинематическое разрешение столкновений для объекта.
    /// Гибрид VehicleCollisionResolver и KinematicMoverWithCollisionFeedback.
    /// Использует BoxCollider для проверки пересечений и алгоритм "collide & slide".
    /// Метод Move рассчитывает скорректированную позицию и поворот, не перемещая сам объект.
    /// Все вычисления выполняются в плоскости XZ (ось Y фиксирована).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class AdvancedVehicleCollisionResolver : MonoBehaviour
    {
        [Header("Collision Settings")]
        [Tooltip("Слой препятствий, с которыми будет происходить столкновение.")]
        public LayerMask collisionLayers = ~0; // Используем имя collisionLayers как в KinematicMover

        [Tooltip("Небольшой отступ от поверхности, чтобы избежать «залипания» или дрожания.")]
        public float skinWidth = 0.01f;

        [Tooltip("Количество подшагов, на которые разбивается общее желаемое движение и вращение за один вызов Move. Увеличивает точность за счет производительности.")]
        public int substeps = 5;

        [Tooltip("Максимальное количество итераций внутри каждого шага депенетрации и скольжения. Ограничивает количество попыток разрешить сложные ситуации столкновений за один подшаг.")]
        public int internalIterations = 3;

        private BoxCollider boxCol;
        private Vector3 cachedColliderScale; // Кэшируем локальный масштаб для корректного размера коллайдера

        void Awake()
        {
            boxCol = GetComponent<BoxCollider>();
            if (boxCol == null)
            {
                Debug.LogError("AdvancedVehicleCollisionResolver: Компонент BoxCollider не найден на объекте " + gameObject.name + "! Функциональность будет нарушена.", this);
                // Можно добавить `enabled = false;` если критично
            }
            // Кэшируем масштаб. Если масштаб объекта может меняться во время игры
            // и это должно влиять на разрешение столкновений, этот кэш нужно будет обновлять.
            // Для большинства транспортных средств масштаб постоянен после инициализации.
            cachedColliderScale = transform.lossyScale;
        }

        /// <summary>
        /// Рассчитывает итоговую позицию и поворот объекта после попытки переместиться из currentPos/currentRot
        /// в desiredPos/desiredRot с учетом столкновений.
        /// Сам объект не перемещает, только возвращает рассчитанные значения.
        /// Все расчеты производятся в плоскости XZ (координата Y фиксируется).
        /// </summary>
        /// <param name="currentPos">Текущее положение объекта.</param>
        /// <param name="currentRot">Текущий поворот объекта.</param>
        /// <param name="desiredPos">Желаемое положение объекта.</param>
        /// <param name="desiredRot">Желаемый поворот объекта.</param>
        /// <returns>Структура MoveResult, содержащая финальную позицию, поворот и информацию о столкновениях.</returns>
        public MoveResult Move(Vector3 currentPos, Quaternion currentRot, Vector3 desiredPos, Quaternion desiredRot)
        {
            MoveResult result = new MoveResult
            {
                CollisionOccurred = false,
                WasMovementFullyBlocked = false,
                MovementBlockSeverity = 0.0f
            };

            if (boxCol == null) // Дополнительная проверка, если Awake не смог найти коллайдер
            {
                Debug.LogError("AdvancedVehicleCollisionResolver: BoxCollider отсутствует, расчет невозможен.", this);
                result.FinalPosition = currentPos; // Возвращаем текущие значения как есть
                result.FinalRotation = currentRot;
                return result;
            }
            
            // Обновляем кэшированный масштаб на случай, если он мог измениться с момента Awake
            // (Это более безопасно, хотя и немного дороже. Если масштаб гарантированно не меняется,
            // можно оставить кэширование только в Awake)
            // cachedColliderScale = transform.lossyScale; // Раскомментируйте, если масштаб может меняться

            // 1. Определяем общее желаемое смещение и относительный поворот
            Vector3 totalDesiredDeltaPosition = desiredPos - currentPos;
            // Относительный поворот, который нужно применить к currentRot, чтобы получить desiredRot
            Quaternion totalDesiredDeltaRotation = Quaternion.Inverse(currentRot) * desiredRot;

            // Сохраняем и фиксируем координату Y
            float fixedY = currentPos.y;

            Vector3 effectiveCurrentPosition = currentPos;
            Quaternion effectiveCurrentRotation = currentRot;

            // 2. Начальная депенетрация (выталкивание из уже существующих пересечений)
            bool initialDepenOccurred;
            effectiveCurrentPosition = Depenetrate(effectiveCurrentPosition, effectiveCurrentRotation, out initialDepenOccurred);
            effectiveCurrentPosition.y = fixedY; // Восстанавливаем Y
            if (initialDepenOccurred) result.CollisionOccurred = true;

            // Сохраняем позицию после начальной депенетрации для расчета общей блокировки движения
            Vector3 positionBeforeSubsteps = effectiveCurrentPosition;

            // 3. Разбиваем движение и поворот на подшаги (substeps)
            // Дельта поворота за один подшаг
            Quaternion stepRotationDelta = Quaternion.SlerpUnclamped(Quaternion.identity, totalDesiredDeltaRotation, 1.0f / substeps);
            // Базовая дельта смещения за один подшаг (только в XZ)
            Vector3 stepTranslationBase = totalDesiredDeltaPosition / substeps;
            stepTranslationBase.y = 0;

            for (int i = 0; i < substeps; i++)
            {
                // 3.1. Применяем поворот для текущего подшага
                effectiveCurrentRotation *= stepRotationDelta;

                // 3.2. Депенетрация после поворота
                bool rotDepenOccurred;
                effectiveCurrentPosition = Depenetrate(effectiveCurrentPosition, effectiveCurrentRotation, out rotDepenOccurred);
                effectiveCurrentPosition.y = fixedY;
                if (rotDepenOccurred) result.CollisionOccurred = true;

                // 3.3. Применяем смещение для текущего подшага (алгоритм Collide and Slide)
                if (stepTranslationBase.sqrMagnitude > 0.000001f) // Если есть желаемое смещение
                {
                    bool slideCollisionOccurred;
                    Vector3 actualSubstepMovement = CollideAndSlideSubstep(effectiveCurrentPosition, effectiveCurrentRotation, stepTranslationBase, out slideCollisionOccurred);
                    effectiveCurrentPosition += actualSubstepMovement;
                    // Y координата не должна меняться из CollideAndSlideSubstep, т.к. desiredSubstepDelta.y = 0
                    effectiveCurrentPosition.y = fixedY; // Дополнительно гарантируем фиксацию Y
                    if (slideCollisionOccurred) result.CollisionOccurred = true;

                    // 3.4. Депенетрация после смещения
                    bool postSlideDepenOccurred;
                    effectiveCurrentPosition = Depenetrate(effectiveCurrentPosition, effectiveCurrentRotation, out postSlideDepenOccurred);
                    effectiveCurrentPosition.y = fixedY;
                    if (postSlideDepenOccurred) result.CollisionOccurred = true;
                }
            }

            // 4. Рассчитываем степень блокировки движения
            Vector3 effectiveDesiredTotalDeltaPositionXZ = totalDesiredDeltaPosition;
            effectiveDesiredTotalDeltaPositionXZ.y = 0; // Интересует только желаемое смещение в XZ

            if (effectiveDesiredTotalDeltaPositionXZ.sqrMagnitude > 0.000001f)
            {
                Vector3 actualTotalDeltaPosition = effectiveCurrentPosition - positionBeforeSubsteps;
                actualTotalDeltaPosition.y = 0; // Сравниваем также в XZ

                float desiredMagnitude = effectiveDesiredTotalDeltaPositionXZ.magnitude;
                Vector3 desiredDirection = effectiveDesiredTotalDeltaPositionXZ.normalized;

                // Проекция фактического смещения на желаемое направление
                float achievedDistanceInDesiredDirection = Vector3.Dot(actualTotalDeltaPosition, desiredDirection);

                // Отношение достигнутого расстояния к желаемому (ограничено от 0 до 1)
                // Если двигались в противоположную сторону или не двигались в желаемом направлении, progressRatio будет <= 0, clamp исправит.
                float progressRatio = Mathf.Clamp01(achievedDistanceInDesiredDirection / desiredMagnitude);

                result.MovementBlockSeverity = 1.0f - progressRatio;
                // Считаем полной блокировкой, если прогресс очень мал (например, менее 5% от желаемого)
                if (result.MovementBlockSeverity > 0.95f)
                {
                    result.WasMovementFullyBlocked = true;
                }
            }
            else // Если не было желаемого движения в XZ, то и блокировки нет
            {
                result.MovementBlockSeverity = 0.0f;
                result.WasMovementFullyBlocked = false;
            }

            result.FinalPosition = effectiveCurrentPosition;
            result.FinalRotation = effectiveCurrentRotation;

            return result;
        }

        /// <summary>
        /// Реализует один подшаг алгоритма "Collide and Slide".
        /// Пытается переместить объект на `desiredSubstepDelta` из `startPosition` с учетом `orientation`.
        /// Использует Physics.BoxCast для обнаружения столкновений. Движение происходит только в плоскости XZ.
        /// </summary>
        /// <param name="startPosition">Начальная позиция для этого подшага.</param>
        /// <param name="orientation">Ориентация объекта.</param>
        /// <param name="desiredSubstepDelta">Желаемое смещение для этого подшага (Y компонента игнорируется).</param>
        /// <param name="hitOccurredGlobal">Выходной параметр: true, если в ходе этого подшага произошло столкновение.</param>
        /// <returns>Фактическое смещение, которое удалось совершить.</returns>
        private Vector3 CollideAndSlideSubstep(Vector3 startPosition, Quaternion orientation, Vector3 desiredSubstepDelta, out bool hitOccurredGlobal)
        {
            hitOccurredGlobal = false;
            // desiredSubstepDelta.y уже должен быть 0 из вызывающего метода
            Vector3 remainingMovement = desiredSubstepDelta;
            Vector3 currentSubstepPosition = startPosition;

            for (int iter = 0; iter < internalIterations; iter++)
            {
                if (remainingMovement.sqrMagnitude < 0.000001f) break; // Движение пренебрежимо мало

                Vector3 boxCenter = currentSubstepPosition + orientation * boxCol.center;
                Vector3 halfExtents = Vector3.Scale(cachedColliderScale, boxCol.size) * 0.5f;

                RaycastHit hitInfo;
                // Бросаем BoxCast на дистанцию текущего остаточного движения
                if (Physics.BoxCast(boxCenter, halfExtents, remainingMovement.normalized, out hitInfo,
                                     orientation, remainingMovement.magnitude, // Дистанция каста
                                     collisionLayers, QueryTriggerInteraction.Ignore))
                {
                    hitOccurredGlobal = true; // Столкновение обнаружено
                    // Перемещаемся до точки столкновения минус skinWidth
                    float moveDist = Mathf.Max(0, hitInfo.distance - skinWidth);
                    currentSubstepPosition += remainingMovement.normalized * moveDist;

                    // Оставшаяся часть вектора desiredSubstepDelta, которую не удалось пройти прямо
                    // Это вектор от точки, где мы остановились, до точки, куда мы изначально хотели попасть в этом шаге,
                    // если бы двигались по прямой remainingMovement.normalized.
                    Vector3 penetrationVectorIfNoStop = remainingMovement.normalized * (remainingMovement.magnitude - moveDist);

                    // Проецируем этот остаток на плоскость столкновения для скольжения
                    remainingMovement = Vector3.ProjectOnPlane(penetrationVectorIfNoStop, hitInfo.normal);
                    remainingMovement.y = 0; // Гарантируем движение только в XZ

                    if (remainingMovement.sqrMagnitude <= 0.000001f) {
                        remainingMovement = Vector3.zero; // Если скольжение невозможно или остаток мал
                        break;
                    }
                }
                else
                {
                    // Столкновений не найдено, перемещаемся на все оставшееся расстояние
                    currentSubstepPosition += remainingMovement;
                    remainingMovement = Vector3.zero; // Движение выполнено
                    break;
                }
            }
            // Возвращаем фактическое смещение от начальной позиции этого подшага
            return currentSubstepPosition - startPosition;
        }

        /// <summary>
        /// Итеративно выталкивает объект из пересечений с другими коллайдерами.
        /// Использует Physics.OverlapBox для обнаружения пересечений и Physics.ComputePenetration для расчета выталкивания.
        /// Выталкивание происходит только в плоскости XZ.
        /// </summary>
        /// <param name="position">Текущая позиция объекта.</param>
        /// <param name="rotation">Текущий поворот объекта.</param>
        /// <param name="depenetrationPerformed">Выходной параметр: true, если было выполнено хотя бы одно выталкивание.</param>
        /// <returns>Новая позиция после попыток депенетрации.</returns>
        private Vector3 Depenetrate(Vector3 position, Quaternion rotation, out bool depenetrationPerformed)
        {
            depenetrationPerformed = false;
            Vector3 currentPos = position;

            for (int iter = 0; iter < internalIterations; iter++)
            {
                Vector3 worldBoxCenter = currentPos + rotation * boxCol.center;
                Vector3 worldHalfExtents = Vector3.Scale(cachedColliderScale, boxCol.size) * 0.5f;
                Collider[] overlaps = Physics.OverlapBox(worldBoxCenter, worldHalfExtents, rotation, collisionLayers, QueryTriggerInteraction.Ignore);

                if (overlaps.Length == 0) break; // Нет пересечений, выходим

                float maxPushDistThisPass = 0f;
                Vector3 bestPushDirThisPass = Vector3.zero;
                bool foundPenetrationToResolveThisPass = false;

                foreach (Collider otherCol in overlaps)
                {
                    if (otherCol == boxCol || otherCol.isTrigger) continue; // Игнорируем сам себя и триггеры

                    if (Physics.ComputePenetration(
                            boxCol, currentPos, rotation,                             // Наш коллайдер
                            otherCol, otherCol.transform.position, otherCol.transform.rotation, // Коллайдер препятствия
                            out Vector3 pushDir, out float pushDist))                 // Результат
                    {
                        // Выбираем наибольшее проникновение для исправления за эту итерацию
                        if (pushDist > maxPushDistThisPass)
                        {
                            maxPushDistThisPass = pushDist;
                            bestPushDirThisPass = pushDir;
                            foundPenetrationToResolveThisPass = true;
                        }
                    }
                }

                if (foundPenetrationToResolveThisPass && maxPushDistThisPass > 0.00001f)
                {
                    // Выталкиваем на расстояние проникновения плюс небольшой отступ (фракция skinWidth)
                    // Это взято из KinematicMoverWithCollisionFeedback
                    Vector3 actualPush = bestPushDirThisPass * (maxPushDistThisPass + skinWidth * 0.1f);
                    actualPush.y = 0; // Выталкивание только в плоскости XZ
                    currentPos += actualPush;
                    depenetrationPerformed = true; // Фиксируем, что депенетрация была выполнена
                }
                else
                {
                    break; // Нет значимых пересечений для исправления или не удалось найти направление
                }
            }
            return currentPos;
        }
    }
}