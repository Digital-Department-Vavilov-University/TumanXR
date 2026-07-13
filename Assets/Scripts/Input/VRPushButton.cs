using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class VRPushButton : MonoBehaviour
{
    [Header("References")]
    public VRTriggerPokeProvider provider;
    
    [Tooltip("Объект подсветки при наведении (Hover - твой белый квад)")]
    public GameObject hoverObject; 
    
    [Tooltip("Объект подсветки при клике (On - твой зеленый квад)")]
    public GameObject clickHighlightObject;

    [Header("Button Meshes")]
    [Tooltip("Верхнее положение кнопки (активно по умолчанию)")]
    public GameObject upObject;  
    
    [Tooltip("Нижнее положение кнопки (включается при нажатии)")]
    public GameObject downObject;  

    [Header("Settings")]
    public Vector3 boxSize = new Vector3(0.1f, 0.1f, 0.1f);
    public float pressVisualDuration = 0.25f;

    [Header("Events")]
    public UnityEvent OnClicked;

    private AudioSource audioSource;
    private bool isHovered = false;

    private void Awake()
    {
        audioSource = GetComponentInChildren<AudioSource>();
        
        // На старте: выключаем обе подсветки и нижнее положение, оставляем только верхнее
        if (hoverObject != null) hoverObject.SetActive(false);
        if (clickHighlightObject != null) clickHighlightObject.SetActive(false);
        if (upObject != null) upObject.SetActive(true);
        if (downObject != null) downObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (provider != null) provider.OnTriggerPulled += HandleTriggerPulled;
    }

    private void OnDisable()
    {
        if (provider != null) provider.OnTriggerPulled -= HandleTriggerPulled;
    }

    private void Update()
    {
        if (provider == null) return;

        // НЕЗАВИСИМАЯ ЛОГИКА HOVER (Белый квад)
        bool currentlyHovered = false;
        
        if (provider.leftPokePoint != null && IsPointInsideBox(provider.leftPokePoint.position))
        {
            currentlyHovered = true;
        }
        else if (provider.rightPokePoint != null && IsPointInsideBox(provider.rightPokePoint.position))
        {
            currentlyHovered = true;
        }

        if (currentlyHovered != isHovered)
        {
            isHovered = currentlyHovered;
            if (hoverObject != null) hoverObject.SetActive(isHovered);
        }
    }

    // НЕЗАВИСИМАЯ ЛОГИКА НАЖАТИЯ
    private void HandleTriggerPulled(Transform pokePoint)
    {
        if (IsPointInsideBox(pokePoint.position))
        {
            if (audioSource != null) audioSource.Play();
            OnClicked?.Invoke();
            
            StartCoroutine(PressRoutine());
        }
    }

    private IEnumerator PressRoutine()
    {
        // Прячем верхний меш, показываем нижний И включаем зеленую подсветку (клик)
        if (upObject != null) upObject.SetActive(false);
        if (downObject != null) downObject.SetActive(true);
        if (clickHighlightObject != null) clickHighlightObject.SetActive(true);

        // Ждем четверть секунды
        yield return new WaitForSeconds(pressVisualDuration);

        // Возвращаем всё как было (зеленая вспышка гаснет, кнопка отжимается обратно)
        if (upObject != null) upObject.SetActive(true);
        if (downObject != null) downObject.SetActive(false);
        if (clickHighlightObject != null) clickHighlightObject.SetActive(false);
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
        Gizmos.color = Color.yellow; 
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
    }
}