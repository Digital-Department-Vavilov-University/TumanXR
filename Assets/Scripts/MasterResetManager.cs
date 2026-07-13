using UnityEngine;
using UnityEngine.Events; // Нужно для работы с UnityEvent

/// <summary>
/// Глобальный менеджер для сброса всех систем симулятора в исходное состояние.
/// </summary>
public class MasterResetManager : MonoBehaviour
{
    [Header("Основные системы для сброса")]
    [Tooltip("Отвечает за возврат транспорта и прицепа на стартовую позицию")]
    public SingleVehicleManager vehicleManager;

    [Tooltip("Отвечает за складывание штанг и выключение полива/эффектов")]
    public SprayerController sprayerController;

    [Tooltip("Отвечает за очистку нарисованной текстуры на поле")]
    public FieldPainter fieldPainter;

    [Header("Дополнительные события (Расширяемость)")]
    [Tooltip("Сюда можно перетащить любые другие методы для сброса прямо в инспекторе, без изменения кода")]
    public UnityEvent onExtraResetActions;

    /// <summary>
    /// Главный публичный метод. Именно его нужно вызывать по нажатию UI-кнопки.
    /// </summary>
    [ContextMenu("Выполнить полный сброс (Reset All)")]
    public void ResetAllSystems()
    {
        Debug.Log("<b>[MasterResetManager]</b> Начинаю глобальный сброс системы...");

        // 1. Сброс физической позиции и состояния транспорта (позиция, скорости и т.д.)
        if (vehicleManager != null)
        {
            vehicleManager.ResetVehicle();
            Debug.Log(" - Транспорт сброшен на стартовую позицию.");
        }
        else
        {
            Debug.LogWarning("[MasterResetManager] SingleVehicleManager не назначен в инспекторе!");
        }

        // 2. Сброс механизмов и эффектов (в SprayerController мы уже вшили сброс BoomController)
        if (sprayerController != null)
        {
            sprayerController.ResetEntireSystem();
            Debug.Log(" - Механизмы свернуты, эффекты полива отключены.");
        }
        else
        {
            Debug.LogWarning("[MasterResetManager] SprayerController не назначен в инспекторе!");
        }

        // 3. Очистка текстуры поля (мокрая земля)
        if (fieldPainter != null)
        {
            fieldPainter.ResetPainter();
            Debug.Log(" - Текстура поля очищена.");
        }
        else
        {
            Debug.LogWarning("[MasterResetManager] FieldPainter не назначен в инспекторе!");
        }

        // 4. Вызов дополнительных кастомных событий
        // Если ты добавишь что-то в блоке onExtraResetActions в инспекторе, оно выполнится здесь.
        if (onExtraResetActions != null)
        {
            onExtraResetActions.Invoke();
        }

        Debug.Log("<b>[MasterResetManager]</b> ГЛОБАЛЬНЫЙ СБРОС УСПЕШНО ЗАВЕРШЕН!");
    }
}