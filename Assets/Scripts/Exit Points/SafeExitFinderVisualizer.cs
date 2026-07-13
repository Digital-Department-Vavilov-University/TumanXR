using UnityEngine;

public class SafeExitFinderVisualizer : MonoBehaviour
{
    // ... Все твои параметры (playerHeight, playerRadius, min/max SearchRadius и т.д.) остаются здесь без изменений ...
    [Header("Параметры игрока")]
    [Tooltip("Высота капсулы игрока")]
    [Range(0.5f, 3f)]
    public float playerHeight = 1.8f;

    [Tooltip("Радиус капсулы игрока")]
    [Range(0.1f, 1f)]
    public float playerRadius = 0.4f;

    [Header("Параметры Поиска")]
    public Collider teleportationArea;
    public LayerMask obstacleLayers;
    [Range(0f, 10f)]
    public float minSearchRadius = 2.0f;
    [Range(1f, 15f)]
    public float maxSearchRadius = 7.0f;
    [Range(50, 500)]
    public int iterations = 150;

    private void OnDrawGizmos()
    {
        if (teleportationArea == null) return;
        
        // 1. Вызываем отладочный метод, чтобы получить все данные
        var searchResult = SafeExitPointFinder.FindSafePoint_WithDebugInfo(
            transform.position, playerHeight, playerRadius, minSearchRadius, maxSearchRadius,
            iterations, teleportationArea, obstacleLayers
        );

        // 2. Рисуем все точки-кандидаты на основе их статуса
        if(searchResult.AllCandidatePoints != null)
        {
            foreach (var pointInfo in searchResult.AllCandidatePoints)
            {
                switch (pointInfo.Status)
                {
                    case SafeExitPointFinder.PointStatus.Valid:
                        Gizmos.color = Color.cyan; // 🔵
                        Gizmos.DrawSphere(pointInfo.Position, 0.07f);
                        break;
                    case SafeExitPointFinder.PointStatus.BlockedByObstacle:
                        Gizmos.color = Color.red; // 🔴
                        Gizmos.DrawSphere(pointInfo.Position, 0.08f);
                        break;
                    case SafeExitPointFinder.PointStatus.OutsideTeleportArea:
                        Gizmos.color = Color.yellow; // 🟡
                        Gizmos.DrawSphere(pointInfo.Position, 0.05f);
                        break;
                }
            }
        }

        // 3. Если найдена финальная точка, рисуем ее отдельно и жирно
        if (searchResult.IsSafePointFound)
        {
            Gizmos.color = Color.green; // ✅
            DrawWireCapsule(searchResult.SafePoint, playerHeight, playerRadius);
            Gizmos.DrawLine(transform.position, searchResult.SafePoint);
        }

        // ... Отрисовка колец мертвой зоны и максимального радиуса ...
        Vector3 originOnGround = transform.position;
        originOnGround.y = 0; // Принудительно ставим Y на уровень пола

        DrawWireDisk(originOnGround, minSearchRadius, new Color(1f, 0.5f, 0f, 0.5f)); // Оранжевый
        DrawWireDisk(originOnGround, maxSearchRadius, new Color(1f, 1f, 1f, 0.5f)); // Белый
    }
    
    // ... Вспомогательные методы DrawWireCapsule и DrawWireDisk остаются здесь без изменений ...
    private void DrawWireCapsule(Vector3 groundPosition, float height, float radius)
    {
        float cylinderHeight = Mathf.Max(0, height - 2 * radius);
        Vector3 bottomCenter = groundPosition + Vector3.up * radius;
        Vector3 topCenter = bottomCenter + Vector3.up * cylinderHeight;
        Gizmos.DrawWireSphere(bottomCenter, radius); Gizmos.DrawWireSphere(topCenter, radius);
        Gizmos.DrawLine(bottomCenter + Vector3.right * radius, topCenter + Vector3.right * radius);
        Gizmos.DrawLine(bottomCenter + Vector3.left * radius, topCenter + Vector3.left * radius);
        Gizmos.DrawLine(bottomCenter + Vector3.forward * radius, topCenter + Vector3.forward * radius);
        Gizmos.DrawLine(bottomCenter + Vector3.back * radius, topCenter + Vector3.back * radius);
    }

    private void DrawWireDisk(Vector3 center, float radius, Color color, int segments = 32)
    {
        Gizmos.color = color;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * (360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}