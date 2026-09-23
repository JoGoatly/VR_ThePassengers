using UnityEngine;

/// <summary>A bus stop created by ForestRoad. Passengers wait at waitPoint.</summary>
public class BusStop : MonoBehaviour
{
    public string stopName;
    [Tooltip("Where the waiting passenger stands, facing the road")]
    public Transform waitPoint;
    [Tooltip("Number of the stop along the route (0 = first)")]
    public int index;
    [Tooltip("Position along the route in metres")]
    public float arcLength;
    [Tooltip("Driving direction of the bus at this stop")]
    public Vector3 roadDirection = Vector3.forward;

    /// <summary>The passenger currently waiting here (null if nobody).</summary>
    public Passenger WaitingPassenger { get; set; }

    /// <summary>A passenger has already been placed here (one per stop).</summary>
    public bool Visited { get; set; }

    void OnDestroy()
    {
        if (WaitingPassenger != null && WaitingPassenger.CurrentState == Passenger.State.Waiting)
            Destroy(WaitingPassenger.gameObject);
    }
}
