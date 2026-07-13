using UnityEngine;
using System.Collections;

public class BoomController : MonoBehaviour
{
    [System.Serializable]
    public class BoomSide
    {
        [Header("Секция A (Основная)")]
        public Transform boomA;
        public Vector3 foldedRotationA;
        public Vector3 unfoldedRotationA;
        public Vector3 forwardAxisA = Vector3.up;
        public float durationA = 2f;

        [Header("Секция B (Дочерняя)")]
        public Transform boomB;
        public Vector3 foldedRotationB;
        public Vector3 unfoldedRotationB;
        public Vector3 forwardAxisB = Vector3.up;
        public float durationB = 1.5f;

        [HideInInspector] public bool isTargetUnfoldedA = false;
        [HideInInspector] public bool isTargetUnfoldedB = false;
        [HideInInspector] public Coroutine coroutineA;
        [HideInInspector] public Coroutine coroutineB;
    }

    [Header("Настройки сторон (Штанги)")]
    public BoomSide leftSide;
    public BoomSide rightSide;

    [Header("Настройки центральной основы")]
    [Tooltip("Ссылка на скрипт-параллелограмм")]
    public ParallelogramLinkage centralBaseLinkage;
    [Tooltip("Время полного опускания/поднимания (от t=1 до t=0)")]
    public float baseMovementDuration = 2.5f;

    [HideInInspector] public bool isTargetBaseLowered = false;
    private Coroutine baseMovementCoroutine;

    [Header("Настройки визуализации (Gizmos)")]
    public float gizmoLength = 3f;

    [Header("Аудио (Звуки механизма)")]
    [Tooltip("Источник звука для петли (поставь галочку Loop в инспекторе)")]
    public AudioSource hydraulicLoopSource;
    [Tooltip("Источник звука для одиночных ударов")]
    public AudioSource clunkSource;
    public AudioClip clunkClip;
    private bool hasAudio = false;

    [Tooltip("Сколько секунд ждать после остановки, прежде чем выключить звук гидравлики")]
    public float audioStopDelay = 0.1f;
    private float currentAudioTimer = 0f;

    // --- Внутренние переменные для отслеживания звука ---
    private Quaternion lastLA, lastLB, lastRA, lastRB;
    private float lastBaseT;
    private bool wasPlayingLoop = false;

    // Состояния: находилась ли деталь в крайней точке в прошлом кадре?
    private bool wasLeftAAtBound, wasLeftBAtBound;
    private bool wasRightAAtBound, wasRightBAtBound;
    private bool wasBaseAtBound;
    private float lastClunkTime = -1f;

    // ==========================================
    // ПУБЛИЧНЫЙ ГЕТТЕР СОСТОЯНИЯ МЕХАНИЗМА
    // ==========================================
    public bool IsFullyDeployed
    {
        get
        {
            // Проверяем левую сторону
            if (leftSide.boomA != null && Quaternion.Angle(leftSide.boomA.localRotation, Quaternion.Euler(leftSide.unfoldedRotationA)) > 1f) return false;
            if (leftSide.boomB != null && Quaternion.Angle(leftSide.boomB.localRotation, Quaternion.Euler(leftSide.unfoldedRotationB)) > 1f) return false;

            // Проверяем правую сторону
            if (rightSide.boomA != null && Quaternion.Angle(rightSide.boomA.localRotation, Quaternion.Euler(rightSide.unfoldedRotationA)) > 1f) return false;
            if (rightSide.boomB != null && Quaternion.Angle(rightSide.boomB.localRotation, Quaternion.Euler(rightSide.unfoldedRotationB)) > 1f) return false;

            // Проверяем основу (t = 0 это опущена/разложена)
            if (centralBaseLinkage != null && centralBaseLinkage.t > 0.01f) return false;

            // Если ни одна из проверок выше не отсеяла нас, значит механизм полностью в боевой готовности
            return true;
        }
    }
    // Объявляем возможные состояния нашего механизма
    public enum MechanismState
    {
        Folded = 1,
        BoomA = 2,
        BoomB = 3,
        BaseLowered = 4
    }

    // Публичный геттер, который возвращает текущее состояние
    public MechanismState CurrentState
    {
        get
        {
            // Вспомогательная локальная функция: проверяет, доехала ли деталь до разложенного состояния 
            // (с погрешностью в 1 градус, чтобы избежать проблем с плавающей запятой)
            bool IsUnfolded(Transform boom, Vector3 unfoldedEuler)
            {
                if (boom == null) return false;
                return Quaternion.Angle(boom.localRotation, Quaternion.Euler(unfoldedEuler)) <= 1f;
            }

            // 1. Проверяем реальное физическое положение всех деталей
            bool baseLowered = centralBaseLinkage != null && centralBaseLinkage.t <= 0.01f;
            
            bool leftBReady = IsUnfolded(leftSide.boomB, leftSide.unfoldedRotationB);
            bool rightBReady = IsUnfolded(rightSide.boomB, rightSide.unfoldedRotationB);
            
            bool leftAReady = IsUnfolded(leftSide.boomA, leftSide.unfoldedRotationA);
            bool rightAReady = IsUnfolded(rightSide.boomA, rightSide.unfoldedRotationA);

            // 2. Определяем состояние сверху вниз (от самого разложенного к сложенному)
            
            // Если база опущена (t почти 0)
            if (baseLowered) 
                return MechanismState.BaseLowered;
                
            // Если хотя бы одна балка Б полностью разложена
            if (leftBReady || rightBReady) 
                return MechanismState.BoomB;
                
            // Если хотя бы одна балка А полностью разложена
            if (leftAReady || rightAReady) 
                return MechanismState.BoomA;

            // Если ничего из вышеперечисленного не разложено до конца — считаем сложенным
            return MechanismState.Folded;
        }
    }

    // Публичное свойство: возвращает true, если механизм сейчас в движении
    public bool IsTransitioning
    {
        get
        {
            // Проверяем базу (t от 0 до 1, если где-то посередине — значит едет)
            if (centralBaseLinkage != null && centralBaseLinkage.t > 0.01f && centralBaseLinkage.t < 0.99f) 
                return true;

            // Локальная функция для проверки отдельной балки
            bool IsBoomMoving(Transform boom, Vector3 foldedEuler, Vector3 unfoldedEuler)
            {
                if (boom == null) return false;
                bool atFolded = Quaternion.Angle(boom.localRotation, Quaternion.Euler(foldedEuler)) < 1f;
                bool atUnfolded = Quaternion.Angle(boom.localRotation, Quaternion.Euler(unfoldedEuler)) < 1f;
                // Если деталь не на старте и не на финише, значит она в пути
                return !atFolded && !atUnfolded;
            }

            // Проверяем все балки
            if (IsBoomMoving(leftSide.boomA, leftSide.foldedRotationA, leftSide.unfoldedRotationA)) return true;
            if (IsBoomMoving(leftSide.boomB, leftSide.foldedRotationB, leftSide.unfoldedRotationB)) return true;
            if (IsBoomMoving(rightSide.boomA, rightSide.foldedRotationA, rightSide.unfoldedRotationA)) return true;
            if (IsBoomMoving(rightSide.boomB, rightSide.foldedRotationB, rightSide.unfoldedRotationB)) return true;

            // Если ни одна проверка не сработала — всё стоит на месте
            return false;
        }
    }    

    // --- КОНТЕКСТНЫЕ МЕНЮ ДЛЯ ШТАНГ ---
    [ContextMenu("ЛЕВАЯ сторона: Считать текущие углы как сложенные")]
    public void SetLeftFolded() { SaveBoomRotations(leftSide, true); }
    [ContextMenu("ЛЕВАЯ сторона: Считать текущие углы как разложенные")]
    public void SetLeftUnfolded() { SaveBoomRotations(leftSide, false); }
    
    [ContextMenu("ПРАВАЯ сторона: Считать текущие углы как сложенные")]
    public void SetRightFolded() { SaveBoomRotations(rightSide, true); }
    [ContextMenu("ПРАВАЯ сторона: Считать текущие углы как разложенные")]
    public void SetRightUnfolded() { SaveBoomRotations(rightSide, false); }

    private void SaveBoomRotations(BoomSide side, bool asFolded)
    {
        if (side.boomA != null) { if(asFolded) side.foldedRotationA = side.boomA.localEulerAngles; else side.unfoldedRotationA = side.boomA.localEulerAngles; }
        if (side.boomB != null) { if(asFolded) side.foldedRotationB = side.boomB.localEulerAngles; else side.unfoldedRotationB = side.boomB.localEulerAngles; }
    }

    void Start()
    {
        ForceFoldSide(leftSide);
        ForceFoldSide(rightSide);
        
        // Принудительно поднимаем основу на старте (t = 1)
        if (centralBaseLinkage != null)
        {
            centralBaseLinkage.t = 1f;
        }
        isTargetBaseLowered = false;

        hasAudio = (hydraulicLoopSource != null) || (clunkSource != null && clunkClip != null);

        // 2. Если аудио не настроено — выходим из Start, дальше ничего не делаем
        if (!hasAudio) return;

        // --- Инициализация для аудио ---
        if (leftSide.boomA != null) lastLA = leftSide.boomA.localRotation;
        if (leftSide.boomB != null) lastLB = leftSide.boomB.localRotation;
        if (rightSide.boomA != null) lastRA = rightSide.boomA.localRotation;
        if (rightSide.boomB != null) lastRB = rightSide.boomB.localRotation;
        if (centralBaseLinkage != null) lastBaseT = centralBaseLinkage.t;

        wasLeftAAtBound = IsAtAnyBound(leftSide.boomA, leftSide.foldedRotationA, leftSide.unfoldedRotationA);
        wasLeftBAtBound = IsAtAnyBound(leftSide.boomB, leftSide.foldedRotationB, leftSide.unfoldedRotationB);
        wasRightAAtBound = IsAtAnyBound(rightSide.boomA, rightSide.foldedRotationA, rightSide.unfoldedRotationA);
        wasRightBAtBound = IsAtAnyBound(rightSide.boomB, rightSide.foldedRotationB, rightSide.unfoldedRotationB);
        wasBaseAtBound = IsBaseAtBound();
    }

    private void ForceFoldSide(BoomSide side)
    {
        if (side.boomA != null) side.boomA.localRotation = Quaternion.Euler(side.foldedRotationA);
        if (side.boomB != null) side.boomB.localRotation = Quaternion.Euler(side.foldedRotationB);
        side.isTargetUnfoldedA = false;
        side.isTargetUnfoldedB = false;
    }

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ UNITY EVENTS ---
    public void ToggleBothA() { ToggleSectionA(leftSide); ToggleSectionA(rightSide); }
    public void ToggleBothB() { ToggleSectionB(leftSide); ToggleSectionB(rightSide); }

    public void ToggleCentralBase()
    {
        if (centralBaseLinkage == null) return;

        // Если мы хотим ОПУСТИТЬ основу, проверяем, разложены ли штанги B
        if (!isTargetBaseLowered)
        {
            if (!IsBoomFullyUnfolded(leftSide) || !IsBoomFullyUnfolded(rightSide))
            {
                Debug.LogWarning("Нельзя опустить основу, пока обе штанги (A и B) не разложены до конца!");
                return;
            }
        }

        isTargetBaseLowered = !isTargetBaseLowered;
        if (baseMovementCoroutine != null) StopCoroutine(baseMovementCoroutine);

        // t=1 это поднятая основа, t=0 это опущенная
        float targetT = isTargetBaseLowered ? 0f : 1f;
        
        baseMovementCoroutine = StartCoroutine(MoveBaseT(targetT, baseMovementDuration));
    }

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ НЕПРЕРЫВНОГО ВВОДА (HOLD - РУЧНОЕ УПРАВЛЕНИЕ) ---
    // unfold = true (раскладываем/опускаем), unfold = false (складываем/поднимаем)
    
    public void StepBothA(float dt, bool unfold)
    {
        StepSectionA(leftSide, dt, unfold);
        StepSectionA(rightSide, dt, unfold);
    }

    public void StepBothB(float dt, bool unfold)
    {
        StepSectionB(leftSide, dt, unfold);
        StepSectionB(rightSide, dt, unfold);
    }

    public void StepCentralBase(float dt, bool lower)
    {
        if (centralBaseLinkage == null) return;

        // Проверка зависимостей (без спама в консоль, чтобы не засорять логи каждый кадр)
        if (lower && (!IsBoomFullyUnfolded(leftSide) || !IsBoomFullyUnfolded(rightSide))) return;

        // Убиваем автоматическую корутину, если она есть
        if (baseMovementCoroutine != null)
        {
            StopCoroutine(baseMovementCoroutine);
            baseMovementCoroutine = null;
        }

        isTargetBaseLowered = lower; // Синхронизируем состояние для Toggle
        
        float targetT = lower ? 0f : 1f; // t=0 опущена, t=1 поднята
        float speed = 1f / baseMovementDuration; // Скорость изменения t в секунду

        centralBaseLinkage.t = Mathf.MoveTowards(centralBaseLinkage.t, targetT, speed * dt);
    }

    // --- ВНУТРЕННЯЯ ЛОГИКА ДЛЯ STEP ---
    private void StepSectionA(BoomSide side, float dt, bool unfold)
    {
        if (side.boomA == null) return;

        // Нельзя сложить А, если B не сложена
        if (!unfold && Quaternion.Angle(side.boomB.localRotation, Quaternion.Euler(side.foldedRotationB)) > 1f) return;

        if (side.coroutineA != null)
        {
            StopCoroutine(side.coroutineA);
            side.coroutineA = null;
        }

        side.isTargetUnfoldedA = unfold;

        Quaternion targetRot = unfold ? Quaternion.Euler(side.unfoldedRotationA) : Quaternion.Euler(side.foldedRotationA);
        float speed = GetAngularSpeed(side.foldedRotationA, side.unfoldedRotationA, side.durationA);
        
        side.boomA.localRotation = Quaternion.RotateTowards(side.boomA.localRotation, targetRot, speed * dt);
    }

    private void StepSectionB(BoomSide side, float dt, bool unfold)
    {
        if (side.boomB == null) return;

        // Нельзя сложить B, пока основа не поднята до конца (t должно быть почти 1)
        if (!unfold && centralBaseLinkage != null && centralBaseLinkage.t < 0.99f) return;
        
        // Нельзя разложить B, пока А не разложена до конца
        if (unfold && Quaternion.Angle(side.boomA.localRotation, Quaternion.Euler(side.unfoldedRotationA)) > 1f) return;

        if (side.coroutineB != null)
        {
            StopCoroutine(side.coroutineB);
            side.coroutineB = null;
        }

        side.isTargetUnfoldedB = unfold;

        Quaternion targetRot = unfold ? Quaternion.Euler(side.unfoldedRotationB) : Quaternion.Euler(side.foldedRotationB);
        float speed = GetAngularSpeed(side.foldedRotationB, side.unfoldedRotationB, side.durationB);
        
        side.boomB.localRotation = Quaternion.RotateTowards(side.boomB.localRotation, targetRot, speed * dt);
    }

    // --- ВНУТРЕННЯЯ ЛОГИКА ШТАНГ ---
    private void ToggleSectionA(BoomSide side)
    {
        if (side.boomA == null) return;

        if (side.isTargetUnfoldedA && Quaternion.Angle(side.boomB.localRotation, Quaternion.Euler(side.foldedRotationB)) > 1f)
        {
            Debug.LogWarning("Сначала нужно полностью сложить штангу B на этой стороне!");
            return; 
        }

        side.isTargetUnfoldedA = !side.isTargetUnfoldedA;
        if (side.coroutineA != null) StopCoroutine(side.coroutineA);
        
        Quaternion targetRot = side.isTargetUnfoldedA ? Quaternion.Euler(side.unfoldedRotationA) : Quaternion.Euler(side.foldedRotationA);
        side.coroutineA = StartCoroutine(RotateBoomConstantSpeed(side.boomA, targetRot, GetAngularSpeed(side.foldedRotationA, side.unfoldedRotationA, side.durationA)));
    }

    private void ToggleSectionB(BoomSide side)
    {
        if (side.boomB == null) return;

        // Если мы хотим СЛОЖИТЬ штангу B, проверяем, поднята ли основа (t должно быть близко к 1)
        if (side.isTargetUnfoldedB && centralBaseLinkage != null)
        {
            if (centralBaseLinkage.t < 0.99f)
            {
                Debug.LogWarning("Нельзя сложить штанги, пока центральная основа не поднята до конца!");
                return;
            }
        }

        if (!side.isTargetUnfoldedB && Quaternion.Angle(side.boomA.localRotation, Quaternion.Euler(side.unfoldedRotationA)) > 1f)
        {
            Debug.LogWarning("Нельзя разложить штангу B, пока штанга А не разложена до конца!");
            return; 
        }

        side.isTargetUnfoldedB = !side.isTargetUnfoldedB;
        if (side.coroutineB != null) StopCoroutine(side.coroutineB);
        
        Quaternion targetRot = side.isTargetUnfoldedB ? Quaternion.Euler(side.unfoldedRotationB) : Quaternion.Euler(side.foldedRotationB);
        side.coroutineB = StartCoroutine(RotateBoomConstantSpeed(side.boomB, targetRot, GetAngularSpeed(side.foldedRotationB, side.unfoldedRotationB, side.durationB)));
    }

    // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ И КОРУТИНЫ ---
    private bool IsBoomFullyUnfolded(BoomSide side)
    {
        if (side.boomB == null) return false;
        return Quaternion.Angle(side.boomB.localRotation, Quaternion.Euler(side.unfoldedRotationB)) < 1f;
    }

    private float GetAngularSpeed(Vector3 folded, Vector3 unfolded, float duration)
    {
        if (duration <= 0f) return 1000f; 
        return Quaternion.Angle(Quaternion.Euler(folded), Quaternion.Euler(unfolded)) / duration;
    }

    private IEnumerator RotateBoomConstantSpeed(Transform boom, Quaternion targetRot, float speedDegPerSec)
    {
        if (boom == null) yield break;
        while (Quaternion.Angle(boom.localRotation, targetRot) > 0.01f)
        {
            boom.localRotation = Quaternion.RotateTowards(boom.localRotation, targetRot, speedDegPerSec * Time.deltaTime);
            yield return null; 
        }
        boom.localRotation = targetRot;
    }

    // Новая корутина для плавного изменения параметра t
    private IEnumerator MoveBaseT(float targetT, float duration)
    {
        if (centralBaseLinkage == null || duration <= 0f) yield break;

        // Скорость изменения параметра t в секунду
        float speed = 1f / duration;

        while (!Mathf.Approximately(centralBaseLinkage.t, targetT))
        {
            // MoveTowards идеально подходит для линейного изменения числа от 0 до 1
            centralBaseLinkage.t = Mathf.MoveTowards(centralBaseLinkage.t, targetT, speed * Time.deltaTime);
            yield return null;
        }
        
        // Гарантируем точное финальное значение
        centralBaseLinkage.t = targetT;
    }

    // ==========================================
    // ЛОГИКА АУДИО (ПАТТЕРН НАБЛЮДАТЕЛЬ)
    // ==========================================
    private void LateUpdate()
    {
        if (!hasAudio) return;
        
        // 1. ПРОВЕРЯЕМ, ЕСТЬ ЛИ ДВИЖЕНИЕ В ЭТОМ КАДРЕ
        bool isMovingNow = false;

        if (HasRotationChanged(leftSide.boomA, ref lastLA)) isMovingNow = true;
        if (HasRotationChanged(leftSide.boomB, ref lastLB)) isMovingNow = true;
        if (HasRotationChanged(rightSide.boomA, ref lastRA)) isMovingNow = true;
        if (HasRotationChanged(rightSide.boomB, ref lastRB)) isMovingNow = true;
        
        if (centralBaseLinkage != null && Mathf.Abs(centralBaseLinkage.t - lastBaseT) > 0.0001f)
        {
            isMovingNow = true;
            lastBaseT = centralBaseLinkage.t;
        }

        // --- ИСПРАВЛЕННАЯ ЛОГИКА ЛУПА ---
        if (isMovingNow)
        {
            currentAudioTimer = audioStopDelay; // Постоянно обновляем таймер, пока едем
            
            if (!wasPlayingLoop)
            {
                if (hydraulicLoopSource != null) hydraulicLoopSource.Play();
                wasPlayingLoop = true;
            }
        }
        else
        {
            // Если остановились, даем звуку небольшую "фору" перед отключением
            if (wasPlayingLoop)
            {
                currentAudioTimer -= Time.deltaTime;
                if (currentAudioTimer <= 0f)
                {
                    if (hydraulicLoopSource != null) hydraulicLoopSource.Stop();
                    wasPlayingLoop = false;
                }
            }
        }

        // 2. ПРОВЕРЯЕМ ФИНАЛЬНЫЕ ТОЧКИ ДЛЯ ЗВУКА УДАРА (CLUNK)
        CheckClunk(leftSide.boomA, leftSide.foldedRotationA, leftSide.unfoldedRotationA, ref wasLeftAAtBound);
        CheckClunk(leftSide.boomB, leftSide.foldedRotationB, leftSide.unfoldedRotationB, ref wasLeftBAtBound);
        CheckClunk(rightSide.boomA, rightSide.foldedRotationA, rightSide.unfoldedRotationA, ref wasRightAAtBound);
        CheckClunk(rightSide.boomB, rightSide.foldedRotationB, rightSide.unfoldedRotationB, ref wasRightBAtBound);

        bool isBaseAtBound = IsBaseAtBound();
        if (isBaseAtBound && !wasBaseAtBound) PlayClunkSound();
        wasBaseAtBound = isBaseAtBound;
    }

    private bool HasRotationChanged(Transform t, ref Quaternion lastRot)
    {
        if (t == null) return false;
        
        // Снизили порог чувствительности с 0.01 до 0.001, чтобы ловить даже микро-движения при 144+ FPS
        if (Quaternion.Angle(t.localRotation, lastRot) > 0.001f)
        {
            lastRot = t.localRotation;
            return true;
        }
        return false;
    }

    private void CheckClunk(Transform t, Vector3 folded, Vector3 unfolded, ref bool wasAtBound)
    {
        bool isAtBound = IsAtAnyBound(t, folded, unfolded);
        
        // Если сейчас мы в крайней точке, а кадр назад НЕ были (только что приехали)
        if (isAtBound && !wasAtBound)
        {
            PlayClunkSound();
        }
        wasAtBound = isAtBound; // Сохраняем для следующего кадра
    }

    private bool IsAtAnyBound(Transform t, Vector3 folded, Vector3 unfolded)
    {
        if (t == null) return false;
        // Погрешность в 0.1 градуса для float
        bool atFolded = Quaternion.Angle(t.localRotation, Quaternion.Euler(folded)) < 0.1f;
        bool atUnfolded = Quaternion.Angle(t.localRotation, Quaternion.Euler(unfolded)) < 0.1f;
        return atFolded || atUnfolded;
    }

    private bool IsBaseAtBound()
    {
        if (centralBaseLinkage == null) return false;
        return centralBaseLinkage.t < 0.01f || centralBaseLinkage.t > 0.99f;
    }

    private void PlayClunkSound()
    {
        // Если с момента последнего удара прошло меньше 0.1 секунды — просто игнорируем вызов
        if (Time.time - lastClunkTime < 0.1f) return;

        // Запоминаем время текущего удара
        lastClunkTime = Time.time;

        if (clunkSource != null && clunkClip != null)
        {
            clunkSource.PlayOneShot(clunkClip);
        }
    }

    [ContextMenu("Сбросить в нуль (Reset)")]
    public void ResetToInitialState()
    {
        // 1. Останавливаем все запущенные корутины движения
        if (baseMovementCoroutine != null) StopCoroutine(baseMovementCoroutine);
        if (leftSide.coroutineA != null) StopCoroutine(leftSide.coroutineA);
        if (leftSide.coroutineB != null) StopCoroutine(leftSide.coroutineB);
        if (rightSide.coroutineA != null) StopCoroutine(rightSide.coroutineA);
        if (rightSide.coroutineB != null) StopCoroutine(rightSide.coroutineB);

        // 2. Моментально складываем штанги (используем уже готовый метод)
        ForceFoldSide(leftSide);
        ForceFoldSide(rightSide);

        // 3. Поднимаем центральную основу
        if (centralBaseLinkage != null)
        {
            centralBaseLinkage.t = 1f;
        }
        isTargetBaseLowered = false;

        // 4. Глушим звук гидравлики
        if (hydraulicLoopSource != null) hydraulicLoopSource.Stop();
        wasPlayingLoop = false;
        currentAudioTimer = 0f;

        // 5. Обновляем переменные слежения (чтобы LateUpdate не подумал, что детали сдвинулись за кадр и не сделал "Клац!")
        if (leftSide.boomA != null) lastLA = leftSide.boomA.localRotation;
        if (leftSide.boomB != null) lastLB = leftSide.boomB.localRotation;
        if (rightSide.boomA != null) lastRA = rightSide.boomA.localRotation;
        if (rightSide.boomB != null) lastRB = rightSide.boomB.localRotation;
        if (centralBaseLinkage != null) lastBaseT = centralBaseLinkage.t;

        wasLeftAAtBound = true;
        wasLeftBAtBound = true;
        wasRightAAtBound = true;
        wasRightBAtBound = true;
        wasBaseAtBound = true;
    }

    // --- ОТРИСОВКА GIZMOS ---
    private void OnDrawGizmosSelected()
    {
        DrawSideGizmos(leftSide);
        DrawSideGizmos(rightSide);
    }

    private void DrawSideGizmos(BoomSide side)
    {
        if (side == null) return;
        DrawBoomGizmo(side.boomA, side.foldedRotationA, side.unfoldedRotationA, side.forwardAxisA, Color.red, Color.green);
        DrawBoomGizmo(side.boomB, side.foldedRotationB, side.unfoldedRotationB, side.forwardAxisB, new Color(1f, 0.5f, 0f), Color.cyan);
    }

    private void DrawBoomGizmo(Transform boom, Vector3 foldedEuler, Vector3 unfoldedEuler, Vector3 forwardAxis, Color colorFolded, Color colorUnfolded)
    {
        if (boom == null) return;
        Quaternion parentRotation = boom.parent != null ? boom.parent.rotation : Quaternion.identity;
        Quaternion worldFolded = parentRotation * Quaternion.Euler(foldedEuler);
        Quaternion worldUnfolded = parentRotation * Quaternion.Euler(unfoldedEuler);
        Vector3 dirFolded = worldFolded * forwardAxis;
        Vector3 dirUnfolded = worldUnfolded * forwardAxis;

        Gizmos.color = colorFolded;
        Gizmos.DrawRay(boom.position, dirFolded * gizmoLength);
        Gizmos.DrawSphere(boom.position + dirFolded * gizmoLength, 0.1f);

        Gizmos.color = colorUnfolded;
        Gizmos.DrawRay(boom.position, dirUnfolded * gizmoLength);
        Gizmos.DrawSphere(boom.position + dirUnfolded * gizmoLength, 0.1f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(boom.position + dirFolded * gizmoLength, boom.position + dirUnfolded * gizmoLength);
    }
}