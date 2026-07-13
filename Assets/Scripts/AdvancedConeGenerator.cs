using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AdvancedConeGenerator : MonoBehaviour
{
    [Header("Настройки формы")]
    [Range(3, 64)] public int segments = 16;
    public float radius = 1f;
    public float height = 2f;

    [Header("Настройки отображения")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(0, 1, 1, 0.5f);

    [ContextMenu("1. Generate Mesh")]
    public void GenerateMesh()
    {
        Mesh mesh = new Mesh { name = "Cone_" + segments + "s" };

        // Нам нужно (segments + 1) вершин для основания и столько же для вершины.
        // Плюс 1 нужен для замыкания UV-развертки (чтобы текстура не ломалась на шве от 1 к 0).
        int numVertices = (segments + 1) * 2;
        Vector3[] vertices = new Vector3[numVertices];
        Vector2[] uvs = new Vector2[numVertices];
        int[] triangles = new int[segments * 3];

        float angleStep = Mathf.PI * 2f / segments;

        for (int i = 0; i <= segments; i++)
        {
            // UV координаты: U идет от 0 до 1 по кругу
            float u = (float)i / segments;
            float angle = i * angleStep;

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // Вершины острия (V = 1, то есть верх текстуры)
            vertices[i] = Vector3.zero;
            uvs[i] = new Vector2(u, 1f);

            // Вершины основания (V = 0, то есть низ текстуры)
            int baseIndex = i + (segments + 1);
            vertices[baseIndex] = new Vector3(x, -height, z);
            uvs[baseIndex] = new Vector2(u, 0f);
        }

        // Собираем треугольники
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = i;                                     // Острие
            triangles[i * 3 + 1] = (i + 1) + (segments + 1);          // Следующая точка основания
            triangles[i * 3 + 2] = i + (segments + 1);                // Текущая точка основания
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

#if UNITY_EDITOR
    [ContextMenu("2. Save Mesh As Asset")]
    public void SaveMeshAsset()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("Сначала сгенерируй меш! (Кликни Generate Mesh)");
            return;
        }

        // Открываем диалоговое окно для сохранения файла
        string path = EditorUtility.SaveFilePanelInProject("Save Mesh Asset", "NewVolumetricCone", "asset", "Save Mesh");
        if (string.IsNullOrEmpty(path)) return;

        // Создаем копию меша, чтобы не сломать инстанс в сцене
        Mesh meshToSave = Instantiate(mf.sharedMesh);
        meshToSave.name = "VolumetricConeMesh";
        
        AssetDatabase.CreateAsset(meshToSave, path);
        AssetDatabase.SaveAssets();

        // Автоматически назначаем сохраненный ассет в MeshFilter
        mf.sharedMesh = meshToSave;
        Debug.Log("Успех! Меш сохранен по пути: " + path);
    }
#endif

    // Рисуем превью в окне Scene
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = gizmoColor;
        Vector3 tip = transform.position;
        float angleStep = Mathf.PI * 2f / segments;
        Vector3 prevBasePos = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            // Вычисляем локальную позицию и переводим в мировые координаты с учетом scale и rotation объекта
            Vector3 localBasePos = new Vector3(MathCos(angle) * radius, -height, MathSin(angle) * radius);
            Vector3 worldBasePos = transform.TransformPoint(localBasePos);

            // Линия от острия к основанию
            Gizmos.DrawLine(tip, worldBasePos);

            // Окружность основания
            if (i > 0) Gizmos.DrawLine(prevBasePos, worldBasePos);
            
            prevBasePos = worldBasePos;
        }
    }

    // Вспомогательные методы для сокращения кода в Gizmos
    private float MathCos(float angle) => Mathf.Cos(angle);
    private float MathSin(float angle) => Mathf.Sin(angle);
}