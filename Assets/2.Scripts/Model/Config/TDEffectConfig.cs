using System;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;

[Serializable]
public class EffectDef
{
    public GameEventKey key;

    [Header("VFX")]
    public GameObject vfxPrefab;       // attack VFX — spawn tại attacker pos khi fire
    public GameObject vfxPrefab2;      // optional second attack VFX (e.g., Victory coins)
    public GameObject impactVfxPrefab; // impact VFX — spawn tại enemy pos khi hit

    [Header("SFX")]
    public AudioClip sfxClip;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Camera Shake")]
    public bool  cameraShake;
    public float shakeStrength = 0.3f;
    public float shakeDuration = 0.25f;
}

[CreateAssetMenu(menuName = "Game Configs/Effect Config", fileName = "Effect Config", order = 3)]
public class TDEffectConfig : ScriptableObject
{
    private static TDEffectConfig m_api;
    public static TDEffectConfig api
        => m_api ??= Resources.Load<TDEffectConfig>(TDConstant.CONFIG_EFFECT);

    [SerializeField] private List<EffectDef> m_Effects = new();

    public List<EffectDef> GetAll() => m_Effects ?? new List<EffectDef>();
}
