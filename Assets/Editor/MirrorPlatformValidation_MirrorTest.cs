using System;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only checks for the platform's server boarding boundaries.</summary>
public static class MirrorPlatformValidation_MirrorTest
{
    [MenuItem("SW/Mirror 테스트/플랫폼 탑승 규칙 검사")]
    public static void Validate()
    {
        Require(!MirrorFourPlayerElevator_MirrorTest.IsAvailable(false, false), "Default hidden node");
        Require(MirrorFourPlayerElevator_MirrorTest.IsAvailable(false, true), "Designated battle node");
        Require(MirrorFourPlayerElevator_MirrorTest.IsAvailable(true, false), "Stage5 always available");
        Require(Waiting(true, true, false, false), "Connected living passenger");
        Require(!Waiting(false, true, false, false), "Disconnected passenger excluded");
        Require(!Waiting(true, false, false, false), "Dead passenger excluded");
        Require(!Waiting(true, true, true, false), "Absent passenger excluded");
        Require(!Waiting(true, true, false, true), "Upper-floor passenger excluded on repeat trip");
        Require(!CanStart(0, 0), "Empty platform never starts");
        Require(!CanStart(4, 3), "Missing passenger waits");
        Require(CanStart(4, 4), "Four passengers depart");
        Require(CanStart(3, 3), "Missing fourth passenger disconnects or dies");
        Require(CanStart(1, 1), "One passenger returns downstairs");
        Require(!CanStart(1, 2), "Duplicate count cannot satisfy boarding");
        Require(!MirrorFourPlayerElevator_MirrorTest.CanStartRide(true, true, 4, 4), "Moving platform rejects second departure");
        Require(!MirrorFourPlayerElevator_MirrorTest.CanStartRide(false, false, 4, 4), "Unavailable platform rejects departure");
        Debug.Log("[MirrorPlatformValidation] 16 platform boarding checks passed. Actual travel, NavMesh landing and disconnect timing still require Play Mode.");
    }

    private static bool Waiting(bool connected, bool alive, bool absent, bool upstairs)
        => MirrorFourPlayerElevator_MirrorTest.IsWaitingParticipant(connected, alive, absent, upstairs);

    private static bool CanStart(int waiting, int boarded)
        => MirrorFourPlayerElevator_MirrorTest.CanStartRide(true, false, waiting, boarded);

    private static void Require(bool condition, string label)
    {
        if (!condition)
            throw new InvalidOperationException("Platform validation failed: " + label);
    }
}
