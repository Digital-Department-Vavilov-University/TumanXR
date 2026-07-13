using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

[System.Serializable]
public class Vehicle
{
    public string name;  // Название транспорта
    public Transform cameraTarget;  // Объект камеры для телепорта
    public PortalManager portalManager;  // Управление кубмапой
    public LoopingAudioManager loopingAudioManager;
    
    [Tooltip("Центр транспорта для расчета точки безопасного выхода")]
    public Transform fakeTransform; // Добавлено для поиска точки выхода
}

public class GameManager : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The Teleportation Provider used to reposition the user. Usually a component on the XR Origin.")]
    TeleportationProvider m_TeleportationProvider;

    [SerializeField]
    [Tooltip("Vehicles managed by the game.")]
    List<Vehicle> vehicles;
    
    [Header("Spawn Settings")]
    [SerializeField]
    private Transform spawnPoint;

    [Header("Настройки безопасного выхода")]
    [Tooltip("Коллайдер, ограничивающий зону для телепортации (например, пол или земля).")]
    [SerializeField] private Collider teleportationArea;

    [Tooltip("Слои, которые считаются препятствиями (транспорт, стены, и т.д.).")]
    [SerializeField] private LayerMask obstacleLayers;

    [Tooltip("Высота капсулы игрока для проверки на столкновения.")]
    [SerializeField] private float playerHeight = 1.8f;

    [Tooltip("Радиус капсулы игрока для проверки на столкновения.")]
    [SerializeField] private float playerRadius = 0.4f;

    [Tooltip("Минимальный радиус от центра транспорта, с которого начинается поиск точки выхода.")]
    [SerializeField] private float minExitRadius = 2.5f;

    [Tooltip("Максимальный радиус от центра транспорта, в котором ищется точка выхода.")]
    [SerializeField] private float maxExitRadius = 8.0f;

    [Tooltip("Количество проверок (плотность спирали поиска).")]
    [SerializeField] private int searchIterations = 200;

    public void MountVehicle(string vehicleName)
    {
        Vehicle vehicle = FindVehicleByName(vehicleName);
        if (vehicle != null)
        {
            Debug.Log($"Mount {vehicleName}!");
            vehicle.portalManager.EnablePortalRendering();
            TeleportPlayerTo(vehicle.cameraTarget.position, vehicle.cameraTarget.rotation);

            if (vehicle.loopingAudioManager != null)
            {
                vehicle.loopingAudioManager.PlayWithFadeIn();
            }
        }
        else
        {
            Debug.LogError($"Vehicle '{vehicleName}' not found!");
        }
    }

    public void DismountVehicle(string vehicleName)
    {
        Vehicle vehicle = FindVehicleByName(vehicleName);
        if (vehicle != null)
        {
            Debug.Log($"Dismount {vehicleName}!");
            
            // Отключаем эффекты
            vehicle.portalManager.DisablePortalRendering();
            if (vehicle.loopingAudioManager != null)
            {
                vehicle.loopingAudioManager.StopWithFadeOut();
            }

            // --- Интеграция поиска безопасной точки ---
            bool safePointFound = false;
            Vector3 finalTeleportPosition = Vector3.zero;
            Quaternion finalTeleportRotation = Quaternion.identity;

            // Убедимся, что задана зона и центр поиска (fakeTransform)
            if (teleportationArea != null && vehicle.fakeTransform != null)
            {
                safePointFound = SafeExitPointFinder.TryFindSafePoint(
                    vehicle.fakeTransform.position,
                    playerHeight,
                    playerRadius,
                    minExitRadius,
                    maxExitRadius,
                    searchIterations,
                    teleportationArea,
                    obstacleLayers,
                    out Vector3 foundPoint
                );

                if (safePointFound)
                {
                    // Точка найдена
                    Debug.Log($"Найдена безопасная точка выхода: {foundPoint}");
                    finalTeleportPosition = foundPoint;

                    // Сделаем так, чтобы игрок смотрел в сторону от транспорта
                    Vector3 lookDirection = (foundPoint - vehicle.fakeTransform.position).normalized;
                    lookDirection.y = 0; // Вращение только по горизонтали
                    
                    // Защита от нулевого вектора (если точка выхода точно совпала с fakeTransform)
                    if (lookDirection != Vector3.zero) 
                    {
                        finalTeleportRotation = Quaternion.LookRotation(lookDirection);
                    }
                }
            }
            else
            {
                 Debug.LogWarning("Не назначен 'teleportationArea' в GameManager или 'fakeTransform' в Vehicle. Поиск безопасной точки невозможен.");
            }

            // НЕУДАЧА: используем запасные варианты
            if (!safePointFound)
            {
                Debug.Log("Безопасная точка не найдена, используем точку спавна.");
                if (spawnPoint != null)
                {
                    finalTeleportPosition = spawnPoint.position;
                    finalTeleportRotation = spawnPoint.rotation;
                }
                else
                {
                    Debug.LogWarning("Точка спавна не задана! Используем (0, 0, 0).");
                    // finalTeleportPosition и Rotation уже проинициализированы нулями в начале
                }
            }
            
            // Выполняем саму телепортацию
            TeleportPlayerTo(finalTeleportPosition, finalTeleportRotation);
        }
        else
        {
            Debug.LogError($"Vehicle '{vehicleName}' not found!");
        }
    }

    void TeleportPlayerTo(Vector3 position, Quaternion rotation)
    {
        TeleportRequest request = new TeleportRequest()
        {
            requestTime = Time.time,
            matchOrientation = MatchOrientation.TargetUpAndForward,
            destinationPosition = position,
            destinationRotation = rotation
        };
        m_TeleportationProvider.QueueTeleportRequest(request);
    }
    
    Vehicle FindVehicleByName(string vehicleName)
    {
        return vehicles.Find(v => v.name.Equals(vehicleName, StringComparison.OrdinalIgnoreCase));
    }
}