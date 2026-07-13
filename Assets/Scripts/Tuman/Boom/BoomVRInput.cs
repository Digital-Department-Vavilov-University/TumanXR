using UnityEngine;

public class BoomVRInput : MonoBehaviour
{
    [Header("Синхронизируемые контроллеры")]
    [Tooltip("Первый BoomController")]
    public BoomController controller1;
    [Tooltip("Второй BoomController")]
    public BoomController controller2;

    [Header("VR Переключатели (Rocker Switches)")]
    [Tooltip("Переключатель для штанги A")]
    public RockerSwitch switchA;
    [Tooltip("Переключатель для штанги B")]
    public RockerSwitch switchB;
    [Tooltip("Переключатель для основы")]
    public RockerSwitch switchBase;

    void Update()
    {
        float dt = Time.deltaTime;

        // ==========================================
        // УПРАВЛЕНИЕ ШТАНГОЙ A
        // ==========================================
        if (switchA != null)
        {
            if (switchA.IsActiveAndPressedA)
            {
                // Кнопка А: Развернуть (Unfold)
                if (controller1 != null) controller1.StepBothA(dt, true);
                if (controller2 != null) controller2.StepBothA(dt, true);
            }
            else if (switchA.IsActiveAndPressedB)
            {
                // Кнопка B: Свернуть (Fold)
                if (controller1 != null) controller1.StepBothA(dt, false);
                if (controller2 != null) controller2.StepBothA(dt, false);
            }
        }

        // ==========================================
        // УПРАВЛЕНИЕ ШТАНГОЙ B
        // ==========================================
        if (switchB != null)
        {
            if (switchB.IsActiveAndPressedA)
            {
                if (controller1 != null) controller1.StepBothB(dt, true);
                if (controller2 != null) controller2.StepBothB(dt, true);
            }
            else if (switchB.IsActiveAndPressedB)
            {
                if (controller1 != null) controller1.StepBothB(dt, false);
                if (controller2 != null) controller2.StepBothB(dt, false);
            }
        }

        // ==========================================
        // УПРАВЛЕНИЕ ОСНОВОЙ (Central Base)
        // ==========================================
        if (switchBase != null)
        {
            if (switchBase.IsActiveAndPressedA)
            {
                // Кнопка А: Опустить (Lower)
                if (controller1 != null) controller1.StepCentralBase(dt, true);
                if (controller2 != null) controller2.StepCentralBase(dt, true);
            }
            else if (switchBase.IsActiveAndPressedB)
            {
                // Кнопка B: Поднять (Raise)
                if (controller1 != null) controller1.StepCentralBase(dt, false);
                if (controller2 != null) controller2.StepCentralBase(dt, false);
            }
        }
    }
}