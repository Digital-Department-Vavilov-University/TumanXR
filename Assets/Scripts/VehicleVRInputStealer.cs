using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Content.Interaction;
using Unity.VRTemplate;

public class VehicleVRInputStealer : MonoBehaviour
{
    public XRJoystick xrJoystick;
    public Unity.VRTemplate.XRKnob xrKnob;
    public float wheelDirection = 1f;

    public float GetSteer()
    {
        return wheelDirection * (2f * xrKnob.value - 1f);
    }

    public float GetThrottle()
    {
        return xrJoystick.value.y;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
