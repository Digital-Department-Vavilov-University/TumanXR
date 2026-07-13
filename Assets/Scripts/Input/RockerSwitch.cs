using UnityEngine;

public class RockerSwitch : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Ссылка на кончик пальца (Poke Point)")]
    public Transform pokePoint;
    
    [Tooltip("Ссылка на провайдер кнопок")]
    public ButtonABProvider buttonProvider;

    [Header("Lights")]
    public GameObject lightA;
    public GameObject lightB;

    [Header("Switch Box Settings")]
    public Vector3 boxSize = new Vector3(0.1f, 0.1f, 0.1f);

    private GameObject lightAWhite;
    private GameObject lightAGreen;
    private GameObject lightBWhite;
    private GameObject lightBGreen;

    // --- НОВЫЕ СВОЙСТВА ДЛЯ ЧТЕНИЯ ИЗВНЕ ---
    // Находится ли палец внутри бокса
    public bool IsHovered { get; private set; } 
    
    // Нажата ли кнопка А при нахождении пальца в боксе
    public bool IsActiveAndPressedA => IsHovered && buttonProvider != null && buttonProvider.IsButtonAPressed;
    
    // Нажата ли кнопка B при нахождении пальца в боксе
    public bool IsActiveAndPressedB => IsHovered && buttonProvider != null && buttonProvider.IsButtonBPressed;

    private void Start()
    {
        if (lightA != null)
        {
            Transform white = lightA.transform.Find("White");
            Transform green = lightA.transform.Find("Green");
            if (white != null) lightAWhite = white.gameObject;
            if (green != null) lightAGreen = green.gameObject;
        }

        if (lightB != null)
        {
            Transform white = lightB.transform.Find("White");
            Transform green = lightB.transform.Find("Green");
            if (white != null) lightBWhite = white.gameObject;
            if (green != null) lightBGreen = green.gameObject;
        }
    }

    private void Update()
    {
        if (pokePoint == null) return;

        // Обновляем статус нахождения пальца в зоне
        IsHovered = IsPointInsideBox(pokePoint.position);

        if (lightA != null && lightA.activeSelf != IsHovered) 
            lightA.SetActive(IsHovered);
            
        if (lightB != null && lightB.activeSelf != IsHovered) 
            lightB.SetActive(IsHovered);

        if (IsHovered && buttonProvider != null)
        {
            UpdateLightColors(lightAWhite, lightAGreen, buttonProvider.IsButtonAPressed);
            UpdateLightColors(lightBWhite, lightBGreen, buttonProvider.IsButtonBPressed);
        }
    }

    private void UpdateLightColors(GameObject whiteQuad, GameObject greenQuad, bool isPressed)
    {
        if (whiteQuad != null && whiteQuad.activeSelf == isPressed) whiteQuad.SetActive(!isPressed);
        if (greenQuad != null && greenQuad.activeSelf != isPressed) greenQuad.SetActive(isPressed);
    }

    private bool IsPointInsideBox(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 extents = boxSize / 2f;
        return Mathf.Abs(localPoint.x) <= extents.x &&
               Mathf.Abs(localPoint.y) <= extents.y &&
               Mathf.Abs(localPoint.z) <= extents.z;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
    }
}