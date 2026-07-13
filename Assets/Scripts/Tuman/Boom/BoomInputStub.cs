using UnityEngine;
using UnityEngine.Events;

public class BoomInputStub : MonoBehaviour
{
    [Header("Ссылка на контроллер")]
    public BoomController controller;

    [Header("Кнопки: РЕЖИМ TOGGLE (Автоматика)")]
    public KeyCode toggleKeyA = KeyCode.U;
    public KeyCode toggleKeyB = KeyCode.I;
    public KeyCode toggleKeyBase = KeyCode.O;

    [Header("Кнопки: РЕЖИМ HOLD (Ручной контроль)")]
    public KeyCode holdUnfoldA = KeyCode.Z;
    public KeyCode holdFoldA = KeyCode.X;
    
    public KeyCode holdUnfoldB = KeyCode.C;
    public KeyCode holdFoldB = KeyCode.V;

    public KeyCode holdLowerBase = KeyCode.B;
    public KeyCode holdRaiseBase = KeyCode.N;

    [Header("События (Unity Events - только для Toggle)")]
    public UnityEvent OnToggleBothA;
    public UnityEvent OnToggleBothB;
    public UnityEvent OnToggleCentralBase;

    void Update()
    {
        if (controller == null) return;

        // ==========================================
        // 1. ПРОВЕРКА РЕЖИМА TOGGLE (Единоразовое нажатие)
        // ==========================================
        if (Input.GetKeyDown(toggleKeyA)) OnToggleBothA?.Invoke();
        if (Input.GetKeyDown(toggleKeyB)) OnToggleBothB?.Invoke();
        if (Input.GetKeyDown(toggleKeyBase)) OnToggleCentralBase?.Invoke();

        // ==========================================
        // 2. ПРОВЕРКА РЕЖИМА HOLD (Удержание кнопки)
        // ==========================================
        float dt = Time.deltaTime;

        // Штанга А
        if (Input.GetKey(holdUnfoldA)) controller.StepBothA(dt, true);
        else if (Input.GetKey(holdFoldA)) controller.StepBothA(dt, false);

        // Штанга B
        if (Input.GetKey(holdUnfoldB)) controller.StepBothB(dt, true);
        else if (Input.GetKey(holdFoldB)) controller.StepBothB(dt, false);

        // Основа
        if (Input.GetKey(holdLowerBase)) controller.StepCentralBase(dt, true);
        else if (Input.GetKey(holdRaiseBase)) controller.StepCentralBase(dt, false);
    }
}