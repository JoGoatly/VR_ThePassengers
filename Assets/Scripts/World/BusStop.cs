using UnityEngine;

/// <summary>A bus stop created by TownBuilder. Passengers wait at waitPoint.</summary>
public class BusStop : MonoBehaviour
{
    public string stopName;
    [Tooltip("Where the waiting passenger stands, facing the road")]
    public Transform waitPoint;
    [Tooltip("Position along the route in metres")]
    public float arcLength;
    [Tooltip("Driving direction of the bus at this stop")]
    public Vector3 roadDirection = Vector3.forward;

    /// <summary>The passenger currently waiting here (null if nobody).</summary>
    public Passenger WaitingPassenger { get; set; }

    /// <summary>Time.time until which no new passenger spawns here.</summary>
    public float CooldownUntil { get; set; }
}
