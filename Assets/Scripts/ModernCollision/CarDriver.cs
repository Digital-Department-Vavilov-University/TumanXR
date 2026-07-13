using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    public class CarDriver : MonoBehaviour
    {
        [Header("References")]
        public CarController car;                           // Логика динамики (Ackermann, CalculateFuturePose)
        public TrailerController trailer;                   // Логика вычисления позы прицепа (ComputeFuturePose)
        public VehicleInputProvider input;                  // Считывание ввода
        [Tooltip("Компонент для кинематического разрешения столкновений для машины")]
        public VehicleCollisionResolver carCollisionResolver;
        [Tooltip("Компонент для кинематического разрешения столкновений для прицепа")]
        public VehicleCollisionResolver trailerCollisionResolver;
        [Tooltip("Компонент для решения хич-констрейнта между машиной и прицепом")]
        public CarTrailerConstraintSolver hitchSolver;
        public Collider[] ignoreColliders;

        void Update()
        {
            float dt = Time.deltaTime;
            float steer = input.GetSteering();    // [-1..1]
            float throttle = input.GetThrottle();   // [-1..1] (газ/тормоз)

            // 1) Рассчитываем желаемую позу машины через CarController (без учёта столкновений)
            var (desiredCarPos, desiredCarRot) = car.CalculateFuturePose(dt, steer, throttle);
            Vector3 currentCarPos = transform.position;
            Quaternion currentCarRot = transform.rotation;

            // 2) Корректируем положение машины с учетом столкновений
            var (resolvedCarPos, resolvedCarRot) = carCollisionResolver.ResolveCollision(
                currentCarPos, currentCarRot, desiredCarPos, desiredCarRot, ignoreColliders);
            car.ApplyPose(resolvedCarPos, resolvedCarRot);

            // 3) Для прицепа: вычисляем желаемую позу через TrailerController.
            //    Будущее положение сцепки машины:
            Transform carHitch = trailer.carHitch;
            Vector3 hitchLocalPos = carHitch.localPosition;
            Vector3 futureCarHitchPos = resolvedCarPos + resolvedCarRot * hitchLocalPos;
            Quaternion hitchLocalRot = carHitch.localRotation;
            Quaternion futureCarHitchRot = resolvedCarRot * hitchLocalRot;
            float futureCarAngleY = futureCarHitchRot.eulerAngles.y;

            var (desiredTrailerPos, desiredTrailerRot, futureTrailerAngle) =
                trailer.ComputeFuturePose(dt, futureCarHitchPos, futureCarAngleY);
            Vector3 currentTrailerPos = trailer.transform.position;
            Quaternion currentTrailerRot = trailer.transform.rotation;

            // 4) Корректируем положение прицепа с учетом столкновений
            var (resolvedTrailerPos, resolvedTrailerRot) = trailerCollisionResolver.ResolveCollision(
                currentTrailerPos, currentTrailerRot, desiredTrailerPos, desiredTrailerRot, ignoreColliders);

            // 5) Решаем хич-констрейнт между машиной и прицепом.
            //    Используем локальные позиции сцепочных точек:
            Vector3 carHitchLocalPos = trailer.carHitch.localPosition;      // точка сцепки на машине
            Vector3 trailerHitchLocalPos = trailer.trailerHitch.localPosition; // точка сцепки на прицепе

            (Vector3 finalCarPos, Quaternion finalCarRot,
             Vector3 finalTrailerPos, Quaternion finalTrailerRot)
                = hitchSolver.SolveHitchConstraint(
                    resolvedCarPos, resolvedCarRot,
                    resolvedTrailerPos, resolvedTrailerRot,
                    carHitchLocalPos, trailerHitchLocalPos);

            // 6) Применяем итоговое состояние
            car.ApplyPose(finalCarPos, finalCarRot);
            trailer.ApplyPose(finalTrailerPos, finalTrailerRot, futureTrailerAngle);
        }
    }
}
