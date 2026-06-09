using System;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;

[Serializable]
public class EffectDef
{
    public GameEventKey key;
    public bool onlySfx; // true → hides the VFX and Camera Shake fields in the Inspector

    // VFX — hidden when onlySfx = true (handled by EffectDefDrawer)
    public GameObject vfxPrefab;       // attack VFX — spawned at the attacker's position when fired
    public GameObject vfxPrefab2;      // optional second attack VFX (e.g., Victory coins)
    public GameObject impactVfxPrefab; // impact VFX — spawned at the enemy's position on hit

    // SFX — always shown
    public AudioClip sfxClip;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // Camera Shake — hidden when onlySfx = true
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
