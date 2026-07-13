using UnityEngine;

[ExecuteAlways]
public class ParallelogramLinkage : MonoBehaviour
{
    [Header("Управление механизмом")]
    [Range(0f, 1f)]
    [Tooltip("Положение механизма: 0 - крайнее нижнее, 1 - крайнее верхнее")]
    public float t = 0.5f;

    [Header("Настройки геометрии прямоугольника")]
    [Tooltip("Ширина неподвижной и подвижной баз")]
    public float rectWidth = 1f;  
    [Tooltip("Высота неподвижной и подвижной баз (расстояние между качелями)")]
    public float rectHeight = 2f; 

    [Header("Настройки соединительных деталей")]
    [Tooltip("Длина подвижных качелей")]
    public float linkLength = 1f; 
    public float minAngle = -60f;
    public float maxAngle = 60f;

    [Header("Объекты (опционально)")]
    public Transform movingRect; // Сюда второй прямоугольник
    public Transform topLink;    // Сюда верхнюю деталь
    public Transform bottomLink; // Сюда нижнюю деталь

    private float CurrentAngle => Mathf.Lerp(minAngle, maxAngle, t);

    void Update()
    {
        UpdateGameObjects();
    }

    private void UpdateGameObjects()
    {
        float angleRad = CurrentAngle * Mathf.Deg2Rad;
        
        Vector3 offset = transform.forward * (linkLength * Mathf.Cos(angleRad)) + 
                         transform.up * (linkLength * Mathf.Sin(angleRad));

        if (movingRect != null)
        {
            movingRect.position = transform.position + offset;
            movingRect.rotation = transform.rotation;
        }

        if (topLink != null)
        {
            // Верхняя деталь теперь смещена вверх на высоту прямоугольника (rectHeight)
            topLink.position = transform.position + transform.up * rectHeight;
            topLink.rotation = Quaternion.LookRotation(offset);
        }

        if (bottomLink != null)
        {
            bottomLink.position = transform.position;
            bottomLink.rotation = Quaternion.LookRotation(offset);
        }
    }

    private void OnDrawGizmos()
    {
        float angleRad = CurrentAngle * Mathf.Deg2Rad;
        
        Vector3 offset = transform.forward * (linkLength * Mathf.Cos(angleRad)) + 
                         transform.up * (linkLength * Mathf.Sin(angleRad));

        // Координаты углов первого (неподвижного) прямоугольника
        Vector3 p1_BL = transform.position; 
        Vector3 p1_BR = transform.position + transform.right * rectWidth; 
        Vector3 p1_TL = transform.position + transform.up * rectHeight; 
        Vector3 p1_TR = transform.position + transform.right * rectWidth + transform.up * rectHeight; 

        // Координаты углов второго (подвижного) прямоугольника
        Vector3 p2_BL = p1_BL + offset;
        Vector3 p2_BR = p1_BR + offset;
        Vector3 p2_TL = p1_TL + offset;
        Vector3 p2_TR = p1_TR + offset;

        // Рисуем первый прямоугольник (зеленый - база)
        Gizmos.color = Color.green;
        DrawRect(p1_BL, p1_BR, p1_TR, p1_TL);

        // Рисуем второй прямоугольник (синий - подвижный)
        Gizmos.color = Color.cyan;
        DrawRect(p2_BL, p2_BR, p2_TR, p2_TL);

        // Рисуем подвижные детали (красные)
        Gizmos.color = Color.red;
        DrawLink(p1_TL, p1_TR, p2_TL, p2_TR); // Верхняя деталь
        DrawLink(p1_BL, p1_BR, p2_BL, p2_BR); // Нижняя деталь
    }

    private void DrawRect(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl)
    {
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }

    private void DrawLink(Vector3 baseLeft, Vector3 baseRight, Vector3 movingLeft, Vector3 movingRight)
    {
        Gizmos.DrawLine(baseLeft, movingLeft);
        Gizmos.DrawLine(baseRight, movingRight);
        Gizmos.DrawLine(baseLeft, baseRight);
        Gizmos.DrawLine(movingLeft, movingRight);
        
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f); 
        Gizmos.DrawLine(baseLeft, movingRight);
        Gizmos.DrawLine(baseRight, movingLeft);
        Gizmos.color = Color.red; 
    }
}