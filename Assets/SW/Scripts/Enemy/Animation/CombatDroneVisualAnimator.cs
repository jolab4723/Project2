using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatDroneVisualAnimator : MonoBehaviour
{
    public enum DroneVariant
    {
        D,
        F
    }

    [Header("Binding")]
    [SerializeField] private DroneVariant variant;
    [SerializeField] private Transform visualRoot;

    [Header("Hover")]
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.12f;
    [SerializeField, Min(0.01f)] private float hoverFrequency = 0.7f;
    [SerializeField, Min(0f)] private float pitchAmplitude = 2.2f;
    [SerializeField, Min(0f)] private float rollAmplitude = 3f;
    [SerializeField] private float phaseOffset;

    [Header("Mechanical Parts")]
    [SerializeField] private float propellerSpeed = 1200f;
    [SerializeField] private float radarSpeed = 90f;
    [SerializeField, Min(0f)] private float weaponRecoilDistance = 0.14f;
    [SerializeField, Min(0.01f)] private float weaponRecoilDuration = 0.16f;
    [SerializeField, Min(0f)] private float aimSmoothing = 120f;

    [Header("Prototype Preview")]
    [SerializeField] private bool previewSweepAim = true;
    [SerializeField] private bool previewAutoFire = true;
    [SerializeField, Min(0.1f)] private float previewFireInterval = 2f;
    [SerializeField, Min(0f)] private float previewFireInitialDelay = 0.75f;

    private readonly List<Transform> propellers = new List<Transform>();

    private Quaternion[] propellerBaseRotations = Array.Empty<Quaternion>();
    private Vector3[] propellerBasePositions = Array.Empty<Vector3>();
    private Vector3[] propellerPivotOffsets = Array.Empty<Vector3>();
    private Transform radar;
    private Transform weapon;
    private Vector3 visualBasePosition;
    private Quaternion visualBaseRotation;
    private Quaternion radarBaseRotation;
    private Vector3 weaponBasePosition;
    private Quaternion weaponBaseRotation;
    private float flightTime;
    private float propellerAngle;
    private float radarAngle;
    private float recoilElapsed = float.PositiveInfinity;
    private float nextPreviewFireTime;
    private float targetAimPitch;
    private float targetAimYaw;
    private float currentAimPitch;
    private float currentAimYaw;
    private bool isBound;

    public DroneVariant Variant => variant;
    public Transform VisualRoot => visualRoot;

    public void Configure(DroneVariant droneVariant, Transform root)
    {
        variant = droneVariant;
        visualRoot = root;

        if (variant == DroneVariant.D)
        {
            hoverAmplitude = 0.14f;
            hoverFrequency = 0.64f;
            pitchAmplitude = 2.4f;
            rollAmplitude = 3.2f;
            propellerSpeed = 1150f;
            radarSpeed = 105f;
            weaponRecoilDistance = 0.13f;
            weaponRecoilDuration = 0.18f;
            previewFireInterval = 2.25f;
            previewFireInitialDelay = 0.9f;
        }
        else
        {
            hoverAmplitude = 0.11f;
            hoverFrequency = 0.76f;
            pitchAmplitude = 1.8f;
            rollAmplitude = 2.7f;
            propellerSpeed = 1350f;
            radarSpeed = 0f;
            weaponRecoilDistance = 0.18f;
            weaponRecoilDuration = 0.14f;
            previewFireInterval = 1.15f;
            previewFireInitialDelay = 0.55f;
        }

        previewSweepAim = true;
        previewAutoFire = true;
        Rebind();
    }

    public void SetAimAngles(float pitch, float yaw)
    {
        previewSweepAim = false;
        targetAimPitch = pitch;
        targetAimYaw = yaw;
    }

    public void SetPreviewEnabled(bool sweepAim, bool autoFire)
    {
        previewSweepAim = sweepAim;
        previewAutoFire = autoFire;
    }

    public void PlayWeaponRecoil()
    {
        recoilElapsed = 0f;
    }

public void Rebind()
    {
        ResetBoundTransforms();
        propellers.Clear();
        radar = null;
        weapon = null;
        isBound = false;

        if (visualRoot == null)
        {
            if (transform.childCount == 0)
            {
                return;
            }

            visualRoot = transform.GetChild(0);
        }

        visualBasePosition = visualRoot.localPosition;
        visualBaseRotation = visualRoot.localRotation;

        Transform[] transforms = visualRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == visualRoot)
            {
                continue;
            }

            string partName = candidate.name;
            if (partName.IndexOf("Propeller", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                propellers.Add(candidate);
            }
            else if (partName.IndexOf("Radar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                radar = candidate;
            }
            else if (partName.IndexOf("Rocket_Launcher", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     partName.IndexOf("Heavy_Gun", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                weapon = candidate;
            }
        }

        propellerBaseRotations = new Quaternion[propellers.Count];
        propellerBasePositions = new Vector3[propellers.Count];
        propellerPivotOffsets = new Vector3[propellers.Count];

        for (int i = 0; i < propellers.Count; i++)
        {
            Transform propeller = propellers[i];
            propellerBaseRotations[i] = propeller.localRotation;
            propellerBasePositions[i] = propeller.localPosition;

            MeshFilter meshFilter = propeller.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                propellerPivotOffsets[i] =
                    Vector3.Scale(meshFilter.sharedMesh.bounds.center, propeller.localScale);
            }
        }

        if (radar != null)
        {
            radarBaseRotation = radar.localRotation;
        }

        if (weapon != null)
        {
            weaponBasePosition = weapon.localPosition;
            weaponBaseRotation = weapon.localRotation;
        }

        isBound = true;
    }

    private void OnEnable()
    {
        Rebind();
        flightTime = phaseOffset;
        propellerAngle = 0f;
        radarAngle = 0f;
        recoilElapsed = float.PositiveInfinity;
        nextPreviewFireTime = Time.time + previewFireInitialDelay;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!isBound)
        {
            Rebind();
            if (!isBound)
            {
                return;
            }
        }

        float deltaTime = Time.deltaTime;
        flightTime += deltaTime;

        AnimateHover();
        AnimatePropellers(deltaTime);
        AnimateRadar(deltaTime);
        AnimateWeapon(deltaTime);

        if (previewAutoFire && weapon != null && Time.time >= nextPreviewFireTime)
        {
            PlayWeaponRecoil();
            nextPreviewFireTime = Time.time + previewFireInterval;
        }
    }

    private void AnimateHover()
    {
        float phase = flightTime * hoverFrequency * Mathf.PI * 2f;
        float verticalOffset = Mathf.Sin(phase) * hoverAmplitude;
        float pitch = Mathf.Sin(phase * 0.63f + 0.8f) * pitchAmplitude;
        float roll = Mathf.Sin(phase * 0.47f + 2.1f) * rollAmplitude;

        visualRoot.localPosition = visualBasePosition + Vector3.up * verticalOffset;
        visualRoot.localRotation = visualBaseRotation * Quaternion.Euler(pitch, 0f, roll);
    }

private void AnimatePropellers(float deltaTime)
    {
        propellerAngle = Mathf.Repeat(propellerAngle + propellerSpeed * deltaTime, 360f);

        for (int i = 0; i < propellers.Count; i++)
        {
            Transform propeller = propellers[i];
            float direction = (i & 1) == 0 ? 1f : -1f;
            Quaternion newRotation =
                propellerBaseRotations[i] *
                Quaternion.AngleAxis(propellerAngle * direction, Vector3.up);
            Vector3 pivotOffset = propellerPivotOffsets[i];

            propeller.localRotation = newRotation;
            propeller.localPosition =
                propellerBasePositions[i] +
                propellerBaseRotations[i] * pivotOffset -
                newRotation * pivotOffset;
        }
    }

    private void AnimateRadar(float deltaTime)
    {
        if (radar == null)
        {
            return;
        }

        radarAngle = Mathf.Repeat(radarAngle + radarSpeed * deltaTime, 360f);
        radar.localRotation = radarBaseRotation * Quaternion.AngleAxis(radarAngle, Vector3.up);
    }

    private void AnimateWeapon(float deltaTime)
    {
        if (weapon == null)
        {
            return;
        }

        if (previewSweepAim)
        {
            float sweepPhase = flightTime * 0.65f;
            targetAimPitch = Mathf.Sin(sweepPhase * 0.83f) * (variant == DroneVariant.D ? 4f : 2.5f);
            targetAimYaw = Mathf.Sin(sweepPhase) * (variant == DroneVariant.D ? 7f : 4f);
        }

        currentAimPitch = Mathf.MoveTowards(currentAimPitch, targetAimPitch, aimSmoothing * deltaTime);
        currentAimYaw = Mathf.MoveTowards(currentAimYaw, targetAimYaw, aimSmoothing * deltaTime);

        float recoilOffset = 0f;
        if (recoilElapsed < weaponRecoilDuration)
        {
            recoilElapsed += deltaTime;
            float normalizedTime = Mathf.Clamp01(recoilElapsed / weaponRecoilDuration);
            recoilOffset = Mathf.Sin(normalizedTime * Mathf.PI) * weaponRecoilDistance;
        }

        weapon.localPosition = weaponBasePosition - Vector3.forward * recoilOffset;
        weapon.localRotation =
            weaponBaseRotation * Quaternion.Euler(currentAimPitch, currentAimYaw, 0f);
    }

    private void OnDisable()
    {
        ResetBoundTransforms();
    }

private void ResetBoundTransforms()
    {
        if (!isBound)
        {
            return;
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBasePosition;
            visualRoot.localRotation = visualBaseRotation;
        }

        for (int i = 0; i < propellers.Count && i < propellerBaseRotations.Length; i++)
        {
            if (propellers[i] != null)
            {
                propellers[i].localPosition = propellerBasePositions[i];
                propellers[i].localRotation = propellerBaseRotations[i];
            }
        }

        if (radar != null)
        {
            radar.localRotation = radarBaseRotation;
        }

        if (weapon != null)
        {
            weapon.localPosition = weaponBasePosition;
            weapon.localRotation = weaponBaseRotation;
        }
    }

    private void OnValidate()
    {
        hoverFrequency = Mathf.Max(0.01f, hoverFrequency);
        weaponRecoilDuration = Mathf.Max(0.01f, weaponRecoilDuration);
        previewFireInterval = Mathf.Max(0.1f, previewFireInterval);
    }
}
