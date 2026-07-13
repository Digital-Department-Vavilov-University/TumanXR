using UnityEngine;

public class SixWheelKinematic : MonoBehaviour
{
    [Header("Колеса (Drag & Drop)")]
    public Transform frontLeft;
    public Transform frontRight;
    public Transform middleLeft;
    public Transform middleRight;
    public Transform rearLeft;
    public Transform rearRight;

    [Header("Параметры движения")]
    public float maxSpeed = 10f;          // Максимальная скорость (м/с)
    public float maxSteerAngle = 35f;     // Максимальный угол поворота передних колес (градусы)
    public float acceleration = 5f;       // Как быстро разгоняемся
    public float deceleration = 10f;      // Как быстро тормозим (инерция)

    // Внутренние переменные
    private float currentSpeed = 0f;
    private float currentSteerAngle = 0f;
    
    // Рассчитанные расстояния (плечи)
    private float distToFrontAxle; // L_front
    private float distToRearAxle;  // L_rear

    [Header("Настройки Gizmos")]
    public bool showGizmos = true;
    public bool showICR = true; // Показывать центр вращения

    void Start()
    {
        CalculateDimensions();
    }

    void Update()
    {
        HandleInput();
        UpdateWheelVisuals();
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    // 1. Считаем геометрию машины на старте
    void CalculateDimensions()
    {
        // Находим Z-позиции осей в локальных координатах
        float frontZ = (frontLeft.localPosition.z + frontRight.localPosition.z) / 2f;
        float midZ = (middleLeft.localPosition.z + middleRight.localPosition.z) / 2f;
        float rearZ = (rearLeft.localPosition.z + rearRight.localPosition.z) / 2f;

        // Считаем дистанции от средней оси (Pivot) до передней и задней
        // Abs нужен на случай, если кто-то перепутает оси местами
        distToFrontAxle = Mathf.Abs(frontZ - midZ);
        distToRearAxle = Mathf.Abs(rearZ - midZ);

        Debug.Log($"Геометрия рассчитана: L_Front = {distToFrontAxle:F2}, L_Rear = {distToRearAxle:F2}");

        if (distToFrontAxle < 0.01f) Debug.LogError("Ошибка: Передняя ось совпадает со средней!");
    }

    // 2. Читаем ввод
    void HandleInput()
    {
        // Газ / Тормоз
        float inputGas = Input.GetAxis("Vertical"); // W/S или Стрелки
        float targetSpeed = inputGas * maxSpeed;
        
        // Плавный разгон/торможение (простейшая имитация инерции)
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, 
            (Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed) ? acceleration : deceleration) * Time.deltaTime);

        // Руль
        float inputSteer = Input.GetAxis("Horizontal"); // A/D или Стрелки
        currentSteerAngle = inputSteer * maxSteerAngle;
    }

    // 3. Физика движения (Кинематика)
    void ApplyMovement()
    {
        if (Mathf.Abs(currentSpeed) < 0.01f) return; // Стоим на месте

        // Вычисляем центр средней оси в мировых координатах
        Vector3 midAxleWorldPos = (middleLeft.position + middleRight.position) / 2f;

        // Проверяем, едем ли мы прямо (угол поворота почти 0)
        // Если угол очень мал, радиус стремится к бесконечности -> RotateAround сломается
        if (Mathf.Abs(currentSteerAngle) < 1f)
        {
            // --- Едем ПРЯМО ---
            // Просто двигаем машину вперед вдоль её направления
            transform.Translate(Vector3.forward * currentSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // --- Едем ПО ДУГЕ ---
            
            // 1. Считаем радиус поворота (R)
            // Формула: R = L_front / tan(steer_front)
            float steerRad = currentSteerAngle * Mathf.Deg2Rad;
            float radius = distToFrontAxle / Mathf.Tan(steerRad);

            // 2. Находим точку вращения (ICR) в мире
            // Отступаем от средней оси вправо на величину радиуса.
            // transform.right уже учитывает поворот самой машины.
            Vector3 icrPoint = midAxleWorldPos + transform.right * radius;

            // 3. Считаем, на какой угол (в градусах) мы должны повернуться за этот кадр
            // Длина дуги за кадр = speed * dt
            // Угол (в радианах) = Длина дуги / Радиус
            // Угол = (speed * dt) / radius
            float stepAngleRad = (currentSpeed * Time.fixedDeltaTime) / radius;
            float stepAngleDeg = stepAngleRad * Mathf.Rad2Deg;

            // 4. Вращаем ВЕСЬ объект вокруг найденной точки ICR
            // Vector3.up - ось вращения (вертикальная)
            transform.RotateAround(icrPoint, Vector3.up, stepAngleDeg);
        }
    }

    // 4. Визуализация поворота колес
    void UpdateWheelVisuals()
    {
        // A. Передние колеса - просто берем угол ввода
        float frontAngle = currentSteerAngle;
        Quaternion frontRot = Quaternion.Euler(0, frontAngle, 0);
        
        frontLeft.localRotation = frontRot;
        frontRight.localRotation = frontRot;

        // B. Задние колеса - высчитываем "Согласованный" угол (Ackermann geometry)
        // Формула: tan(rear) = (L_rear / L_front) * tan(front)
        // Знак минус, так как они поворачивают в противофазе
        float frontTan = Mathf.Tan(currentSteerAngle * Mathf.Deg2Rad);
        float rearTan = (distToRearAxle / distToFrontAxle) * frontTan;
        
        // Арктангенс возвращает радианы -> в градусы. И ставим минус.
        float rearAngle = -Mathf.Atan(rearTan) * Mathf.Rad2Deg;
        Quaternion rearRot = Quaternion.Euler(0, rearAngle, 0);

        rearLeft.localRotation = rearRot;
        rearRight.localRotation = rearRot;

        // C. Средние колеса всегда прямо (или можно добавить им вращение при езде, но не поворот)
        middleLeft.localRotation = Quaternion.identity;
        middleRight.localRotation = Quaternion.identity;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        if (frontLeft == null || frontRight == null || middleLeft == null || middleRight == null || rearLeft == null || rearRight == null) return;

        // Если мы в редакторе и не играем, рассчитаем дистанции, чтобы видеть их сразу
        if (!Application.isPlaying)
        {
            float fZ = (frontLeft.localPosition.z + frontRight.localPosition.z) / 2f;
            float mZ = (middleLeft.localPosition.z + middleRight.localPosition.z) / 2f;
            float rZ = (rearLeft.localPosition.z + rearRight.localPosition.z) / 2f;
            distToFrontAxle = Mathf.Abs(fZ - mZ);
            distToRearAxle = Mathf.Abs(rZ - mZ);
        }

        // 1. Рисуем оси (Синие)
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(frontLeft.position, frontRight.position);
        Gizmos.DrawLine(middleLeft.position, middleRight.position);
        Gizmos.DrawLine(rearLeft.position, rearRight.position);

        // 2. Рисуем "Позвоночник" (Зеленые) - показывает базы L_front и L_rear
        Gizmos.color = Color.green;
        Vector3 frontCenter = (frontLeft.position + frontRight.position) / 2f;
        Vector3 midCenter = (middleLeft.position + middleRight.position) / 2f;
        Vector3 rearCenter = (rearLeft.position + rearRight.position) / 2f;

        Gizmos.DrawLine(midCenter, frontCenter);
        Gizmos.DrawLine(midCenter, rearCenter);

        // 3. Рисуем Центр Вращения (ICR) и радиусы
        if (showICR && Mathf.Abs(currentSteerAngle) > 1f) // Рисуем только если есть поворот > 1 градуса
        {
            // Считаем радиус поворота R = L / tan(angle)
            // Угол берем передний, так как он ведущий в расчетах
            float steerRad = currentSteerAngle * Mathf.Deg2Rad;
            float radius = distToFrontAxle / Mathf.Tan(steerRad);

            // Центр вращения всегда находится на линии средней оси (так как она неповоротная)
            // В локальных координатах это (radius, 0, 0) относительно midCenter
            // Примечание: Если руль вправо (+), радиус положительный, центр справа. Все верно.
            Vector3 icrLocalPoint = new Vector3(radius, 0, 0); 
            
            // Переводим локальную точку смещения в мировые координаты относительно СРЕДНЕЙ ОСИ
            // Важно использовать TransformPoint от машины, но с учетом смещения средней оси, если она не в 0,0,0
            // Но проще взять Right вектор машины
            Vector3 icrWorldPosition = midCenter + transform.right * radius;

            // Рисуем точку центра вращения
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(icrWorldPosition, 0.5f);

            // Рисуем лучи от каждой оси к центру вращения
            // Если математика верна, все оси должны смотреть строго перпендикулярно этим лучам
            Gizmos.color = new Color(1, 0, 1, 0.5f); // Полупрозрачный маджента
            Gizmos.DrawLine(frontCenter, icrWorldPosition);
            Gizmos.DrawLine(midCenter, icrWorldPosition);
            Gizmos.DrawLine(rearCenter, icrWorldPosition);
        }
    }
}