using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyVehicleSystem.ModernCollision;

public class VRVehicleInputProvider : VehicleInputProvider
{
        public VehicleVRInputStealer vehicleVRInputStealer;
        public override float GetSteering()
        {
            return vehicleVRInputStealer.GetSteer();
        }

        public override float GetThrottle()
        {
            return vehicleVRInputStealer.GetThrottle();
        }
}
