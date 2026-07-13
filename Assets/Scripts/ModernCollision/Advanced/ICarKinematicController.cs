using UnityEngine;

namespace MyVehicleSystem.ModernCollision
{
    public interface ICarKinematicController
    {
        // --- Simulation ---
        void UpdateDynamics(float dt, float steerInput, float throttleInput);
        (Vector3 futurePosition, Quaternion futureRotation) CalculateFuturePose(float dt);
        void ApplyCollisionFeedback(MoveResult result);

        // --- Pose ---
        void ApplyPose(Vector3 position, Quaternion rotation);
        void TeleportAndResetState(Vector3 position, Quaternion rotation);

        // --- Visuals (runtime sim) ---
        void UpdateWheelVisuals(float dt);

        // --- Visuals (network / interpolation) ---
        void ApplyWheelVisualState(
            float visualSteerDeg,
            float flRollDeg, float frRollDeg,
            float rlRollDeg, float rrRollDeg
        );

        // --- Read-only visual state ---
        float GetVisualSteerAngleDeg();
        float GetFLWheelRollDeg();
        float GetFRWheelRollDeg();
        float GetRLWheelRollDeg();
        float GetRRWheelRollDeg();

        // --- Unity bridge ---
        Transform transform { get; }
        GameObject gameObject { get; }
    }
}
