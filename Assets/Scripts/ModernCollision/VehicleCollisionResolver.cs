using UnityEngine;
using System.Collections.Generic;

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Отвечает за кинематическое разрешение столкновений для объекта (например, машинки или прицепа).
    /// Использует BoxCollider для проверки пересечений и алгоритм "collide & slide".
    /// Работает как чистая функция: получает текущее и желаемое состояние и возвращает скорректированное.
    /// Дополнительно принимает список игнорируемых коллайдеров.
    /// При этом все вычисления выполняются в плоскости XZ (ось Y фиксирована).
    /// </summary>
    public class VehicleCollisionResolver : MonoBehaviour
    {
        [Header("Collision Settings")]
        [Tooltip("Небольшой запас, чтобы не «залипнуть» у стен")]
        public float skinWidth = 0.01f;
        [Tooltip("Максимальное число итераций (подшагов) за один кадр")]
        public int maxIterations = 5;
        [Tooltip("Слой препятствий")]
        public LayerMask collisionMask;

        private BoxCollider boxCollider;
        private Vector3 colliderScale;

        void Awake()
        {
            boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                Debug.LogError("На объекте " + gameObject.name + " не найден BoxCollider для VehicleCollisionResolver!");
            }
            colliderScale = transform.lossyScale;
        }

        /// <summary>
        /// Принимает текущее состояние (позицию и поворот) и желаемое состояние,
        /// затем, разбивая перемещение и поворот на подшаги, корректирует положение с учётом столкновений.
        /// Дополнительно принимает список коллайдеров, которые необходимо игнорировать.
        /// Работает только в плоскости XZ (ось Y не изменяется).
        /// </summary>
        /// <param name="currentPos">Текущее положение объекта</param>
        /// <param name="currentRot">Текущий поворот объекта</param>
        /// <param name="desiredPos">Желаемое положение объекта</param>
        /// <param name="desiredRot">Желаемый поворот объекта</param>
        /// <param name="ignoreColliders">Список коллайдеров, которые необходимо игнорировать</param>
        /// <returns>Кортеж (скорректированное положение, скорректированный поворот)</returns>
        public (Vector3, Quaternion) ResolveCollision(
            Vector3 currentPos,
            Quaternion currentRot,
            Vector3 desiredPos,
            Quaternion desiredRot,
            Collider[] ignoreColliders = null)
        {
            // Зафиксируем исходное значение Y
            float fixedY = currentPos.y;

            Vector3 totalMovement = desiredPos - currentPos;
            // Обнуляем вертикальную составляющую
            totalMovement = new Vector3(totalMovement.x, 0, totalMovement.z);

            float currentY = currentRot.eulerAngles.y;
            float desiredY = desiredRot.eulerAngles.y;
            float totalRotation = Mathf.DeltaAngle(currentY, desiredY);

            int subSteps = maxIterations;
            Vector3 stepMovement = totalMovement / subSteps;
            float stepRotation = totalRotation / subSteps;

            Vector3 pos = currentPos;
            Quaternion rot = currentRot;

            // Сначала попробуем выдавить объект, если он уже оказался в проникновении
            (pos, rot) = ResolvePenetrations(pos, rot, ignoreColliders);
            // Фиксируем Y
            pos = new Vector3(pos.x, fixedY, pos.z);

            for (int i = 0; i < subSteps; i++)
            {
                // --- Поворот ---
                Quaternion newRot = rot * Quaternion.Euler(0, stepRotation, 0);
                (Vector3 posAfterRot, Quaternion rotAfterRot) = ResolvePenetrations(pos, newRot, ignoreColliders);
                pos = posAfterRot;
                rot = rotAfterRot;
                pos = new Vector3(pos.x, fixedY, pos.z);

                // --- Движение (Collide & Slide) ---
                Vector3 moveDelta = CollideAndSlide(pos, rot, stepMovement, ignoreColliders);
                Vector3 posAfterMove = pos + moveDelta;
                (Vector3 posAfterMoveResolved, Quaternion rotAfterMove) = ResolvePenetrations(posAfterMove, rot, ignoreColliders);
                pos = posAfterMoveResolved;
                rot = rotAfterMove;
                pos = new Vector3(pos.x, fixedY, pos.z);
            }

            // Гарантируем, что Y не изменился
            pos = new Vector3(pos.x, fixedY, pos.z);
            return (pos, rot);
        }

        /// <summary>
        /// Реализует алгоритм Collide & Slide для небольшого шага движения.
        /// Использует BoxCastAll для поиска столкновений, проецируя движение вдоль плоскости препятствия.
        /// Дополнительно принимает список игнорируемых коллайдеров.
        /// Все расчёты проводятся с обнулённой вертикальной составляющей.
        /// </summary>
        private Vector3 CollideAndSlide(Vector3 position, Quaternion rotation, Vector3 stepMovement, Collider[] ignoreColliders = null)
        {
            // Обнуляем вертикальную составляющую исходного шага
            stepMovement = new Vector3(stepMovement.x, 0, stepMovement.z);

            Vector3 remaining = stepMovement;
            Vector3 resultMove = Vector3.zero;
            int iteration = 0;

            while (remaining.magnitude > 0.0001f && iteration < maxIterations)
            {
                Vector3 boxCenter = position + rotation * boxCollider.center;
                Vector3 halfExtents = Vector3.Scale(boxCollider.size, colliderScale) * 0.5f;

                RaycastHit[] hits = Physics.BoxCastAll(
                    boxCenter,
                    halfExtents,
                    remaining.normalized,
                    rotation,
                    remaining.magnitude + skinWidth,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);

                // Отфильтровываем игнорируемые коллайдеры и выбираем ближайший hit
                RaycastHit? validHit = null;
                foreach (var hit in hits)
                {
                    if (ShouldIgnoreCollider(hit.collider, ignoreColliders))
                        continue;
                    if (!validHit.HasValue || hit.distance < validHit.Value.distance)
                    {
                        validHit = hit;
                    }
                }

                if (!validHit.HasValue)
                {
                    // Если столкновений не найдено, перемещаемся на оставшееся расстояние
                    resultMove += remaining;
                    break;
                }
                else
                {
                    float moveDist = validHit.Value.distance - skinWidth;
                    if (moveDist < 0f)
                        moveDist = 0f;
                    Vector3 moveStep = remaining.normalized * moveDist;
                    // Обнуляем Y компонента
                    moveStep = new Vector3(moveStep.x, 0, moveStep.z);
                    resultMove += moveStep;

                    float usedFrac = (remaining.magnitude > 0f) ? (moveDist / remaining.magnitude) : 1f;
                    float leftoverDist = remaining.magnitude - (remaining.magnitude * usedFrac);
                    Vector3 leftover = remaining.normalized * leftoverDist;
                    // Проецируем остаток на горизонтальную плоскость
                    leftover = Vector3.ProjectOnPlane(leftover, validHit.Value.normal);
                    leftover = new Vector3(leftover.x, 0, leftover.z);
                    remaining = leftover;
                }

                iteration++;
            }

            // Гарантируем, что смещение не имеет вертикальной составляющей
            resultMove = new Vector3(resultMove.x, 0, resultMove.z);
            return resultMove;
        }

        /// <summary>
        /// Итеративно проверяет и корректирует положение объекта, если он находится в проникновении с препятствиями.
        /// Использует OverlapBox и ComputePenetration.
        /// Дополнительно принимает список игнорируемых коллайдеров.
        /// Корректировка применяется только по осям X и Z.
        /// </summary>
        private (Vector3, Quaternion) ResolvePenetrations(Vector3 position, Quaternion rotation, Collider[] ignoreColliders = null)
        {
            Vector3 pos = position;
            Quaternion rot = rotation;
            int iteration = 0;
            bool penetrationExists = true;

            while (penetrationExists && iteration < maxIterations)
            {
                penetrationExists = false;
                Vector3 boxCenter = pos + rot * boxCollider.center;
                Vector3 halfExtents = Vector3.Scale(boxCollider.size, colliderScale) * 0.5f;
                Collider[] overlaps = Physics.OverlapBox(
                    boxCenter,
                    halfExtents,
                    rot,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);

                foreach (Collider col in overlaps)
                {
                    if (ShouldIgnoreCollider(col, ignoreColliders))
                        continue;

                    Vector3 direction;
                    float distance;
                    bool overlapped = Physics.ComputePenetration(
                        boxCollider, pos, rot,
                        col, col.transform.position, col.transform.rotation,
                        out direction, out distance);

                    if (overlapped && distance > 0f)
                    {
                        // Проецируем направление на плоскость XZ (убираем вертикальную составляющую)
                        Vector3 horizontalDir = new Vector3(direction.x, 0, direction.z);
                        if (horizontalDir.sqrMagnitude > 0.0001f)
                        {
                            horizontalDir = horizontalDir.normalized;
                            pos += horizontalDir * (distance + skinWidth);
                            penetrationExists = true;
                        }
                    }
                }
                iteration++;
            }
            return (pos, rot);
        }

        /// <summary>
        /// Вспомогательный метод для проверки, следует ли игнорировать данный коллайдер.
        /// </summary>
        private bool ShouldIgnoreCollider(Collider col, Collider[] ignoreColliders)
        {
            if (ignoreColliders == null)
                return false;

            foreach (Collider ignore in ignoreColliders)
            {
                if (col == ignore)
                    return true;
            }
            return false;
        }
    }
}
