using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enviro;

public class AddictionalLightsController : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Список всех источников света (Point Lights, Spot Lights), которыми нужно управлять.")]
    public List<Light> controlledLights;

    [Tooltip("Время включения света (например, 19.5f - это 19:30).")]
    [Range(0f, 24f)]
    public float turnOnTime = 19.0f; // 7 PM

    [Tooltip("Время выключения света (например, 6.5f - это 06:30).")]
    [Range(0f, 24f)]
    public float turnOffTime = 7.0f; // 7 AM

    // Приватная переменная для отслеживания текущего состояния света
    private bool areLightsOn = false;

    // Start is called before the first frame update
    void Start()
    {
        // При запуске сцены один раз проверяем время и устанавливаем правильное состояние света.
        // Это нужно на случай, если игра начнется ночью.
        float initialTime = Enviro.EnviroManager.instance.Time.GetUniversalTimeOfDay();
        bool shouldBeOn = IsNightTime(initialTime);
        SetLightsState(shouldBeOn);
        areLightsOn = shouldBeOn;
    }

    // Update is called once per frame
    void Update()
    {
        // Получаем текущее время от Enviro
        float currentTime = Enviro.EnviroManager.instance.Time.GetUniversalTimeOfDay();

        // Определяем, должно ли быть включено освещение в данный момент
        bool shouldBeOn = IsNightTime(currentTime);
        
        // Проверяем, изменилось ли требуемое состояние света по сравнению с текущим.
        // Это оптимизация: мы будем вызывать функцию переключения только один раз
        // в момент, когда день сменяется ночью (или наоборот), а не каждый кадр.
        if (shouldBeOn != areLightsOn)
        {
            SetLightsState(shouldBeOn);
            areLightsOn = shouldBeOn; // Обновляем наше сохраненное состояние
        }
    }

    /// <summary>
    /// Проверяет, является ли указанное время ночным.
    /// </summary>
    /// <param name="time">Время в формате float (часы.доли_часа)</param>
    /// <returns>true, если ночь, иначе false</returns>
    private bool IsNightTime(float time)
    {
        // Логика для ночи, которая переходит через полночь (например, с 19:00 до 07:00)
        if (turnOnTime > turnOffTime)
        {
            return time >= turnOnTime || time < turnOffTime;
        }
        // Логика для случая, если "ночь" находится в пределах одного дня (например, с 01:00 до 05:00)
        else
        {
            return time >= turnOnTime && time < turnOffTime;
        }
    }

    /// <summary>
    /// Включает или выключает все источники света в списке.
    /// </summary>
    /// <param name="state">true для включения, false для выключения.</param>
    private void SetLightsState(bool state)
    {
        // Если список пуст, выходим, чтобы не получить ошибку
        if (controlledLights == null || controlledLights.Count == 0)
        {
            Debug.LogWarning("Список источников света (Controlled Lights) не назначен в инспекторе!");
            return;
        }
        
        Debug.Log(state ? "Включаем ночное освещение..." : "Выключаем ночное освещение...");

        // Проходим по всему списку и включаем/выключаем компонент Light
        foreach (Light lightSource in controlledLights)
        {
            if (lightSource != null)
            {
                lightSource.enabled = state;
            }
        }
    }
}