using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Статический класс для поиска безопасной точки выхода из транспорта.
/// Содержит методы для игрового использования и для отладочной визуализации.
/// </summary>
public static class SafeExitPointFinder
{
    /// <summary>
    /// Статус проверенной точки-кандидата.
    /// </summary>
    public enum PointStatus
    {
        Valid,                // Точка подходит
        BlockedByObstacle,    // Точка заблокирована препятствием
        OutsideTeleportArea   // Точка находится вне разрешенной зоны
    }

    /// <summary>
    /// Структура для хранения детальной информации о точке-кандидате (для отладки).
    /// </summary>
    public struct CandidatePointInfo
    {
        public Vector3 Position;
        public PointStatus Status;
    }

    /// <summary>
    /// Класс для хранения полного результата поиска (для отладки).
    /// </summary>
    public class SearchResult
    {
        public bool IsSafePointFound;
        public Vector3 SafePoint;
        public List<CandidatePointInfo> AllCandidatePoints = new List<CandidatePointInfo>();
    }

    /// <summary>
    /// [Для отладки] Находит безопасную точку и возвращает ПОЛНУЮ информацию о всех проверенных точках.
    /// Этот метод медленнее, так как не останавливается после нахождения первой точки.
    /// </summary>
    public static SearchResult FindSafePoint_WithDebugInfo(Vector3 origin, float playerHeight, float playerRadius, float minSearchRadius, float maxSearchRadius, int iterations, Collider teleportArea, LayerMask obstacleLayers)
    {
        var result = new SearchResult();
        origin.y = 0;

        for (int i = 0; i < iterations; i++)
        {
            float normalizedProgress = (float)i / iterations;
            float distance = Mathf.Lerp(minSearchRadius, maxSearchRadius, normalizedProgress);
            float angle = i * 0.2f;
            Vector3 candidatePoint = origin + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);

            float capsuleCylinderHeight = Mathf.Max(0, playerHeight - 2 * playerRadius);
            Vector3 capsuleBottom = candidatePoint + Vector3.up * playerRadius;
            Vector3 capsuleTop = capsuleBottom + Vector3.up * capsuleCylinderHeight;

            if (Physics.CheckCapsule(capsuleBottom, capsuleTop, playerRadius, obstacleLayers))
            {
                result.AllCandidatePoints.Add(new CandidatePointInfo { Position = candidatePoint, Status = PointStatus.BlockedByObstacle });
                continue;
            }

            if (teleportArea.Raycast(new Ray(candidatePoint + Vector3.up, Vector3.down), out RaycastHit hitInfo, 2.0f))
            {
                result.AllCandidatePoints.Add(new CandidatePointInfo { Position = hitInfo.point, Status = PointStatus.Valid });
                if (!result.IsSafePointFound)
                {
                    result.IsSafePointFound = true;
                    result.SafePoint = hitInfo.point;
                }
            }
            else
            {
                result.AllCandidatePoints.Add(new CandidatePointInfo { Position = candidatePoint, Status = PointStatus.OutsideTeleportArea });
            }
        }
        return result;
    }

    /// <summary>
    /// [Для игры] Быстро находит первую доступную безопасную точку.
    /// Возвращает true и точку через out-параметр в случае успеха.
    /// </summary>
    public static bool TryFindSafePoint(Vector3 origin, float playerHeight, float playerRadius, float minSearchRadius, float maxSearchRadius, int iterations, Collider teleportArea, LayerMask obstacleLayers, out Vector3 safePoint)
    {
        origin.y = 0;
        safePoint = origin; // Значение по умолчанию

        for (int i = 0; i < iterations; i++)
        {
            float normalizedProgress = (float)i / iterations;
            float distance = Mathf.Lerp(minSearchRadius, maxSearchRadius, normalizedProgress);
            float angle = i * 0.2f;
            Vector3 candidatePoint = origin + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);

            float capsuleCylinderHeight = Mathf.Max(0, playerHeight - 2 * playerRadius);
            Vector3 capsuleBottom = candidatePoint + Vector3.up * playerRadius;
            Vector3 capsuleTop = capsuleBottom + Vector3.up * capsuleCylinderHeight;

            if (Physics.CheckCapsule(capsuleBottom, capsuleTop, playerRadius, obstacleLayers))
            {
                continue;
            }

            if (teleportArea.Raycast(new Ray(candidatePoint + Vector3.up, Vector3.down), out RaycastHit hitInfo, 2.0f))
            {
                safePoint = hitInfo.point;
                return true; // Нашли! Сразу выходим.
            }
        }
        
        return false; // Не нашли.
    }
}