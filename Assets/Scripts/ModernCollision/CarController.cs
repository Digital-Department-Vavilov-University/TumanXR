using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    /// <summary>
    /// Отвечает за хранение состояния и кинематику машинки:
    ///  - Углы поворота колёс (Ackermann)
    ///  - Позиция, поворот
    ///  - Расчёт будущего положения по входам (CalculateFuturePose)
    ///  - Применение (ApplyPose)
    /// При этом класс сам не занимается чтением ввода (Input) и проверкой коллизий.
    /// </summary>
    public class CarController : MonoBehaviour
    {
        [Header("Wheel References (Visual Steering)")]
        public Transform frontLeftWheel;
        public Transform frontRightWheel;
        public Transform rearLeftWheel;
        public Transform rearRightWheel;

        [Header("Car Geometry & Speed")]
        public float maxSteerAngle = 30f;
        public float wheelbase     = 2.5f;
        public float trackWidth    = 1.5f;
        public float moveSpeed     = 10f;

        // Храним внутреннее состояние, чтобы между кадрами у нас была "преемственность".
        private float _thetaInner;
        private float _thetaOuter;

        /// <summary>
        /// Обновляет углы передних колёс для визуализации руления.
        /// Не изменяет transform. Теперь этот метод приватный и вызывается исключительно внутри самого класса CarController
        /// </summary>
        private void UpdateSteeringAngles(float steeringInput)
        {
            // steeringInput предполагается в диапазоне [-1..1], умножим на maxSteerAngle.
            float steerAngle = steeringInput * maxSteerAngle;

            if (Mathf.Abs(steerAngle) < 0.001f)
            {
                // Почти прямое движение
                _thetaInner = 0f;
                _thetaOuter = 0f;
                if (frontLeftWheel)  frontLeftWheel.localRotation  = Quaternion.identity;
                if (frontRightWheel) frontRightWheel.localRotation = Quaternion.identity;
                return;
            }

            float turnRadiusBase = wheelbase / Mathf.Tan(Mathf.Deg2Rad * steerAngle);

            // Определяем, в какую сторону руление (лево/право):
            if (steerAngle > 0f)
            {
                // Поворот направо: inner wheel = правое колесо
                _thetaInner = Mathf.Atan2(wheelbase, turnRadiusBase - trackWidth / 2) * Mathf.Rad2Deg;
                _thetaOuter = Mathf.Atan2(wheelbase, turnRadiusBase + trackWidth / 2) * Mathf.Rad2Deg;

                if (frontRightWheel) frontRightWheel.localRotation = Quaternion.Euler(0f, _thetaInner, 0f);
                if (frontLeftWheel)  frontLeftWheel.localRotation  = Quaternion.Euler(0f, _thetaOuter, 0f);
            }
            else
            {
                // Поворот налево: inner wheel = левое колесо
                _thetaInner = Mathf.Atan2(wheelbase, turnRadiusBase + trackWidth / 2) * Mathf.Rad2Deg;
                _thetaOuter = Mathf.Atan2(wheelbase, turnRadiusBase - trackWidth / 2) * Mathf.Rad2Deg;

                if (frontLeftWheel)  frontLeftWheel.localRotation  = Quaternion.Euler(0f, _thetaInner, 0f);
                if (frontRightWheel) frontRightWheel.localRotation = Quaternion.Euler(0f, _thetaOuter, 0f);
            }
        }

        /// <summary>
        /// По заданным входам (steeringInput, throttleInput) и dt 
        /// рассчитывает будущие (position, rotation) для машинки, не изменяя transform.
        /// </summary>
        /// <returns>(futurePos, futureRot)</returns>
        public (Vector3, Quaternion) CalculateFuturePose(float dt, float steeringInput, float throttleInput)
        {
            // Сначала применяем формулы Ackermann, чтобы узнать _thetaInner/_thetaOuter
            UpdateSteeringAngles(steeringInput);

            // Далее продолжаем старым образом
            Vector3 oldPos = transform.position;
            Quaternion oldRot = transform.rotation;

            // 1) Определяем дистанцию (вперёд/назад).
            float distance = throttleInput * moveSpeed * dt;

            // 2) Будущее положение (без учёта поворота).
            Vector3 forwardDir = oldRot * Vector3.forward;
            Vector3 newPos = oldPos + forwardDir * distance;

            // 3) Считаем усреднённый угол поворота передних колёс (Ackermann).
            float avgSteer = (_thetaInner + _thetaOuter) * 0.5f;
            Quaternion newRot = oldRot;

            if (Mathf.Abs(avgSteer) > 0.001f)
            {
                float turnRadius = wheelbase / Mathf.Tan(Mathf.Deg2Rad * avgSteer);
                float angularVel = distance / turnRadius;  // в радианах за кадр
                float deltaAngleDeg = angularVel * Mathf.Rad2Deg;
                newRot = oldRot * Quaternion.Euler(0f, deltaAngleDeg, 0f);
            }

            return (newPos, newRot);
        }

        /// <summary>
        /// Применяет рассчитанные (position, rotation) к реальному transform, 
        /// чтобы машинка сместилась на сцене. 
        /// При этом в данном простом подходе внутри класса 
        /// мы не храним каких-то «предыдущих поз» — оно не обязательно для самой машинки.
        /// Если нужно – можно добавить сохранение state внутри класса.
        /// </summary>
        public void ApplyPose(Vector3 newPos, Quaternion newRot)
        {
            transform.SetPositionAndRotation(newPos, newRot);
        }
    }
}
