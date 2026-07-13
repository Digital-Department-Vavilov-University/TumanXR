using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class VRToggleButton : MonoBehaviour
{
    [Header("References")]
    public VRTriggerPokeProvider provider;
    
    [Tooltip("Объект, активный при нахождении пальца в зоне (Hover)")]
    public GameObject offObject; 
    
    [Tooltip("Объект, который вспыхивает при нажатии на 0.25 сек")]
    public GameObject onObject;  

    [Header("Settings")]
    public Vector3 boxSize = new Vector3(0.1f, 0.1f, 0.1f);
    public float pressVisualDuration = 0.25f;

    [Header("Events")]
    public UnityEvent OnToggled;

    private AudioSource audioSource;
    private bool isHovered = false;

    private void Awake()
    {
        audioSource = GetComponentInChildren<AudioSource>();
        
        // На старте выключаем оба визуала
        if (offObject != null) offObject.SetActive(false);
        if (onObject != null) onObject.SetActive(false);
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

        // 1. НЕЗАВИСИМАЯ ЛОГИКА HOVER (Наведение)
        bool currentlyHovered = false;
        
        if (provider.leftPokePoint != null && IsPointInsideBox(provider.leftPokePoint.position))
        {
            currentlyHovered = true;
        }
        else if (provider.rightPokePoint != null && IsPointInsideBox(provider.rightPokePoint.position))
        {
            currentlyHovered = true;
        }

        // Включаем/выключаем offObject только в момент изменения состояния (вход/выход из зоны)
        if (currentlyHovered != isHovered)
        {
            isHovered = currentlyHovered;
            if (offObject != null) offObject.SetActive(isHovered);
        }
    }

    // 2. НЕЗАВИСИМАЯ ЛОГИКА CLICK (Нажатие)
    private void HandleTriggerPulled(Transform pokePoint)
    {
        // Проверяем, что нажал именно тот палец, который сейчас находится в боксе
        if (IsPointInsideBox(pokePoint.position))
        {
            if (audioSource != null) audioSource.Play();
            OnToggled?.Invoke();
            
            // Запускаем корутину показа On_object
            StartCoroutine(ShowOnObjectRoutine());
        }
    }

    private IEnumerator ShowOnObjectRoutine()
    {
        if (onObject != null)
        {
            onObject.SetActive(true); // Включаем
            yield return new WaitForSeconds(pressVisualDuration); // Ждем
            onObject.SetActive(false); // Выключаем
        }
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
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
    }
}