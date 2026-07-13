using UnityEngine;

public class FieldPainter : MonoBehaviour
{
    [Header("Холст (Render Texture)")]
    public RenderTexture paintMask;

    [Header("Настройки кисти (Поливалки)")]
    [Tooltip("Центр кисти (пустышка, которая привязана к трактору/штанге)")]
    public Transform drawCenter;
    [Tooltip("Размеры кисти (X - ширина размаха штанг, Y - толщина линии)")]
    public Vector2 brushSize = new Vector2(20f, 2f);

    [Header("Настройки поля")]
    [Tooltip("Центр твоего поля (Transform)")]
    public Transform fieldCenter;
    [Tooltip("Физические размеры поля в метрах (X - ширина, Y - длина)")]
    public Vector2 fieldSize = new Vector2(100f, 100f);

    [Header("Состояние")]
    public bool isPainting = false;

    private Material paintMaterial;
    private Vector3 lastLeftPos;
    private Vector3 lastRightPos;
    private bool wasPaintingLastFrame = false;

    public void SetPaintingState(bool state)
    {
        isPainting = state;
    }

    private void Start()
    {
        // Создаем материал для заливки
        paintMaterial = new Material(Shader.Find("Sprites/Default"));
        ClearTexture();
    }

    public void ClearTexture()
    {
        if (paintMask == null) return;
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = paintMask;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = currentRT;
    }

    private void LateUpdate()
    {
        if (paintMask == null || drawCenter == null || fieldCenter == null) return;

        if (!isPainting)
        {
            wasPaintingLastFrame = false;
            return;
        }

        // 1. Вычисляем крайние точки "ширины" кисти (левую и правую) относительно центра и вращения
        Vector3 halfWidth = drawCenter.right * (brushSize.x * 0.5f);
        Vector3 currentLeft = drawCenter.position - halfWidth;
        Vector3 currentRight = drawCenter.position + halfWidth;

        // 2. Вычисляем 4 угла самого прямоугольника кисти (для отрисовки её толщины/высоты)
        Vector3 halfHeight = drawCenter.forward * (brushSize.y * 0.5f);
        Vector3 frontLeft = currentLeft + halfHeight;
        Vector3 backLeft = currentLeft - halfHeight;
        Vector3 frontRight = currentRight + halfHeight;
        Vector3 backRight = currentRight - halfHeight;

        // Перехватываем управление рендером
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = paintMask;
        paintMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadOrtho();
        GL.Begin(GL.TRIANGLES);
        GL.Color(Color.white);

        // --- РИСУЕМ ---
        
        // А) Рисуем сам прямоугольник кисти (текущее положение)
        DrawQuad(frontLeft, frontRight, backRight, backLeft);

        // Б) Рисуем "шлейф" от прошлого кадра к текущему, чтобы не было разрывов на высокой скорости
        if (wasPaintingLastFrame)
        {
            DrawQuad(lastLeftPos, lastRightPos, currentRight, currentLeft);
        }

        GL.End();
        GL.PopMatrix();
        
        RenderTexture.active = currentRT; // Возвращаем рендер обратно

        // Запоминаем точки для следующего кадра
        lastLeftPos = currentLeft;
        lastRightPos = currentRight;
        wasPaintingLastFrame = true;
    }

    // Вспомогательный метод: рисует четырехугольник из 4 точек
    private void DrawQuad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    {
        Vector2 uv1 = WorldToUV(p1);
        Vector2 uv2 = WorldToUV(p2);
        Vector2 uv3 = WorldToUV(p3);
        Vector2 uv4 = WorldToUV(p4);

        // Первый треугольник
        GL.Vertex3(uv1.x, uv1.y, 0);
        GL.Vertex3(uv2.x, uv2.y, 0);
        GL.Vertex3(uv3.x, uv3.y, 0);

        // Второй треугольник
        GL.Vertex3(uv1.x, uv1.y, 0);
        GL.Vertex3(uv3.x, uv3.y, 0);
        GL.Vertex3(uv4.x, uv4.y, 0);
    }

    // Перевод из 3D мира в 2D координаты текстуры (0.0 - 1.0)
    private Vector2 WorldToUV(Vector3 worldPos)
    {
        // 1. Находим мировой вектор от центра поля до кисти
        Vector3 offset = worldPos - fieldCenter.position;

        // 2. Магия Dot Product: проецируем этот вектор на оси поля (right и forward).
        // Это дает нам точное расстояние в метрах, учитывая вращение поля, но ИГНОРИРУЯ его Scale!
        float localX = Vector3.Dot(offset, fieldCenter.right);
        float localZ = Vector3.Dot(offset, fieldCenter.forward);

        // 3. Нормализуем координаты в диапазон от 0 до 1
        float u = (localX / fieldSize.x) + 0.5f;
        float v = (localZ / fieldSize.y) + 0.5f; 

        // Инверсия для корректного отображения (оставляем как было)
        u = 1.0f - u; 
        v = 1.0f - v; 

        return new Vector2(u, v);
    }

    // ==========================================
    // ВИЗУАЛИЗАЦИЯ ДЛЯ ОТЛАДКИ (GIZMOS)
    // ==========================================
    private void OnDrawGizmos()
    {
        // 1. Рисуем границы ПОЛЯ
        if (fieldCenter != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f); 
            
            // ТЕПЕРЬ УЧИТЫВАЕМ ВРАЩЕНИЕ ПОЛЯ: вместо Quaternion.identity берем fieldCenter.rotation
            Gizmos.matrix = Matrix4x4.TRS(fieldCenter.position, fieldCenter.rotation, Vector3.one);
            
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(fieldSize.x, 0.05f, fieldSize.y));
        }

        // 2. Рисуем прямоугольник КИСТИ (с учетом её вращения)
        if (drawCenter != null)
        {
            Gizmos.color = Color.cyan; // Голубой цвет для кисти
            
            // Фиксируем матрицу с учетом позиции И ВРАЩЕНИЯ кисти
            Gizmos.matrix = Matrix4x4.TRS(drawCenter.position, drawCenter.rotation, Vector3.one);
            
            // Рисуем ориентированный прямоугольник
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(brushSize.x, 0.1f, brushSize.y));
        }
    }

    // ==========================================
    // СБРОС СИСТЕМЫ (RESET)
    // ==========================================
    [ContextMenu("Сбросить холст в нуль (Reset)")]
    public void ResetPainter()
    {
        // 1. Принудительно выключаем процесс рисования и сбрасываем шлейф
        isPainting = false;
        wasPaintingLastFrame = false;

        // 2. Вызываем твой же метод для заливки текстуры черным цветом
        ClearTexture();

        Debug.Log("RenderTexture поля полностью очищена и сброшена!");
    }
}