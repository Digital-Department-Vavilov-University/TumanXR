using UnityEngine;

public class SprayerController : MonoBehaviour
{
    [Header("Ссылки на системы")]
    public BoomController boomController;
    public FieldPainter fieldPainter;

    [Header("Настройки эффектов")]
    [Tooltip("Если выключено, методы MistController вызываться не будут")]
    public bool useMist = true;
    [Tooltip("Если включено, будут работать фонтанчики (конусы)")]
    public bool useFountains = true;

    [Header("Визуальные эффекты (Дымка)")]
    public DualMistControllerSimple leftMist;
    public DualMistControllerSimple rightMist;

    [Header("Визуальные эффекты (Фонтанчики)")]
    public FountainController leftFountains;
    public FountainController rightFountains;

    [Header("Аудио")]
    public AudioSource loopAudioSource;
    public AudioSource oneShotAudioSource;
    public AudioClip startClip;
    public AudioClip stopClip;

    public bool IsSpraying { get; private set; } = false;

    private void Update()
    {
        if (IsSpraying && boomController != null)
        {
            if (!boomController.IsFullyDeployed)
            {
                Debug.Log("Механизм сдвинулся! Автоматическое отключение полива.");
                ForceStopSpraying(); // Оставляем звук для аварийного отключения
            }
        }
    }

    public void ToggleSpraying()
    {
        if (IsSpraying) ForceStopSpraying();
        else TryStartSpraying();
    }

    private void TryStartSpraying()
    {
        if (boomController == null || !boomController.IsFullyDeployed)
        {
            Debug.LogWarning("Отказ: Нельзя включить полив!");
            return;
        }

        IsSpraying = true;

        if (fieldPainter != null) fieldPainter.SetPaintingState(true);

        // Звуки
        if (oneShotAudioSource != null && startClip != null) 
            oneShotAudioSource.PlayOneShot(startClip);
        if (loopAudioSource != null) 
            loopAudioSource.Play();

        // Эффекты
        if (useMist)
        {
            if (leftMist != null) leftMist.PlayMist();
            if (rightMist != null) rightMist.PlayMist();
        }
        if (useFountains)
        {
            if (leftFountains != null) leftFountains.PlayFountains();
            if (rightFountains != null) rightFountains.PlayFountains();
        }
    }

    // ИЗМЕНЕНИЕ: Добавлен параметр playAudio, который по умолчанию true
    private void ForceStopSpraying(bool playAudio = true)
    {
        if (!IsSpraying) return;

        IsSpraying = false;

        if (fieldPainter != null) fieldPainter.SetPaintingState(false);

        // Звуки: проигрываем стоп-клип только если playAudio == true
        if (playAudio && oneShotAudioSource != null && stopClip != null) 
        {
            oneShotAudioSource.PlayOneShot(stopClip);
        }
        
        if (loopAudioSource != null) 
            loopAudioSource.Stop();

        // Остановка визуальных эффектов
        if (useMist)
        {
            if (leftMist != null) leftMist.StopMist();
            if (rightMist != null) rightMist.StopMist();
        }
        if (useFountains)
        {
            if (leftFountains != null) leftFountains.StopFountains();
            if (rightFountains != null) rightFountains.StopFountains();
        }
    }

    [ContextMenu("Сбросить всю систему (Reset)")]
    public void ResetEntireSystem()
    {
        // 1. Выключаем полив БЕЗ проигрывания звука стопа
        ForceStopSpraying(false);

        // 2. Передаем команду механике свернуться
        if (boomController != null)
        {
            boomController.ResetToInitialState();
        }
        
        Debug.Log("Система полностью сброшена в нулевое состояние (тихий режим)!");
    }
}