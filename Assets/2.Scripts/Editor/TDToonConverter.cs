using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Polish A1: every model a match shows shades with Toon Shaders Pro. Each source material gets one
/// toon twin under OUTPUT_DIR, cloned from the Knight's toon material so the set reads as one; only
/// the colour texture, the base tint and alpha clipping come from the source.
/// Pack materials are never edited: prefabs are re-pointed at the twins.
/// </summary>
public static class TDToonConverter
{
    public const string TEMPLATE_PATH = "Assets/1.Assets/Materials/Toon/Toon_OperatorKnight.mat";
    public const string OUTPUT_DIR = "Assets/1.Assets/Materials/Toon";
    public const string TOON_SHADER = "Toon Shaders Pro/URP/Toon";
    public const string OUTLINE_LAYER = "Outlined";
    private const string GAMEPLAY_SCENE = "Assets/3.Scenes/DTGamePlay.unity";

    // Gates glow on purpose, their label is text, and the selection ring is its own effect; the
    // backdrop mountains and the sky dome are unlit silhouettes that no light should shade. Synty's
    // triplanar shader projects grass over dirt in world space, which a UV-mapped toon twin flattens.
    private static readonly string[] k_Exempt =
    {
        "Universal Render Pipeline/Unlit", "TextMeshPro/Mobile/Distance Field", "Custom/SelectionIndicator",
        "Synty/Generic_ParticlesUnlit", "Unlit/Texture", "Synty/PolygonShader",
    };

    // Sidekick keeps its colours in _ColorMap, URP Lit in _BaseMap, Synty in _Albedo_Map.
    private static readonly string[] k_AlbedoProps = { "_ColorMap", "_BaseMap", "_Albedo_Map", "_MainTex" };

    public static bool IsExempt(Material m) => m != null && k_Exempt.Contains(m.shader.name);

    public static Texture AlbedoOf(Material src)
    {
        foreach (var p in k_AlbedoProps)
            if (src.HasProperty(p) && src.GetTexture(p) != null) return src.GetTexture(p);
        return null;
    }

    // One twin per source, found by name on later runs, so re-running changes nothing.
    public static Material ToonFor(Material src, Material template)
    {
        if (src == null || src.shader.name == TOON_SHADER || IsExempt(src)) return src;
        string path = $"{OUTPUT_DIR}/Toon_{src.name}.mat";
        var toon = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (toon != null) return toon;

        toon = new Material(template) { name = $"Toon_{src.name}" };
        toon.SetTexture("_BaseMap", AlbedoOf(src));
        string tint = src.HasProperty("_BaseColor") ? "_BaseColor" : src.HasProperty("_Color") ? "_Color" : null;
        toon.SetColor("_BaseColor", tint != null ? src.GetColor(tint) : Color.white);

        // A cut-out source left opaque renders its transparent texels as solid squares.
        bool clip = (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f) || src.IsKeywordEnabled("_ALPHATEST_ON");
        toon.SetFloat("_AlphaClip", clip ? 1f : 0f);
        if (clip)
        {
            toon.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
            toon.EnableKeyword("_ALPHATEST_ON");
        }
        else toon.DisableKeyword("_ALPHATEST_ON");

        AssetDatabase.CreateAsset(toon, path);
        return toon;
    }

    public static int Apply(GameObject prefabAsset, Material template)
    {
        string path = AssetDatabase.GetAssetPath(prefabAsset);
        var root = PrefabUtility.LoadPrefabContents(path);
        int swapped = 0;
        try
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                var mats = r.sharedMaterials;
                int before = swapped;
                for (int i = 0; i < mats.Length; i++)
                {
                    var toon = ToonFor(mats[i], template);
                    if (toon == mats[i]) continue;
                    mats[i] = toon;
                    swapped++;
                }
                if (swapped > before) r.sharedMaterials = mats;
            }
            if (swapped > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return swapped;
    }

    // The outline volume draws only this layer. A renderer whose every material is exempt (the selection
    // ring) stays where it is, so it gets no outline.
    public static int SetOutlineLayer(GameObject prefabAsset)
    {
        int layer = LayerMask.NameToLayer(OUTLINE_LAYER);
        if (layer < 0) throw new System.InvalidOperationException($"layer {OUTLINE_LAYER} missing");
        string path = AssetDatabase.GetAssetPath(prefabAsset);
        var root = PrefabUtility.LoadPrefabContents(path);
        int moved = 0;
        try
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.sharedMaterials.All(IsExempt) || r.gameObject.layer == layer) continue;
                r.gameObject.layer = layer;
                moved++;
            }
            if (moved > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return moved;
    }

    // The game's own configs say what a match spawns; ammo is shared between turrets.
    public static List<GameObject> UnitPrefabs()
    {
        var list = new List<GameObject>();
        var ops = Load<TDFlyweightOperatorDataSettings>()?.GetAllOperators();
        if (ops != null) list.AddRange(ops.Select(o => o.operatorPrefab));
        var enemies = Load<TDFlyweightEnemyDataSettings>()?.GetAllEnemies();
        if (enemies != null) list.AddRange(enemies.Select(e => e.prefab));
        var towers = Load<TDFlyweightTowerDataSettings>()?.GetAllTowers();
        if (towers != null)
        {
            list.AddRange(towers.Select(t => t.towerPrefab));
            list.AddRange(towers.Select(t => t.ammoPrefab));
        }
        return list.Where(p => p != null).Distinct().ToList();
    }

    // Stages pick tiles and props through a visual config; the gameplay scene wires the defaults the
    // others fall back on. Units are UnitPrefabs' business, so they are left out here.
    public static List<GameObject> MapPrefabs()
    {
        // The default road tile reaches the scene through TDResourceObject, by this name, not as a reference.
        var list = new List<GameObject> { AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/1.Assets/Resources/Prefabs/{TDConstant.PREFAB_PATH}.prefab") };
        var stages = Load<TDStageRepository>()?.GetAll();
        if (stages != null)
            foreach (var v in stages.Where(s => s != null && s.VisualConfig != null).Select(s => s.VisualConfig).Distinct())
            {
                list.Add(v.PathTilePrefab);
                list.Add(v.TowerZonePrefab);
                list.Add(v.ObstacleTilePrefab);
                if (v.ObstaclePrefabs != null) list.AddRange(v.ObstaclePrefabs);
            }

        foreach (var dep in AssetDatabase.GetDependencies(GAMEPLAY_SCENE, false))
        {
            if (!dep.EndsWith(".prefab")) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(dep);
            if (go != null && go.GetComponentsInChildren<Renderer>(true).Any(r => r is MeshRenderer || r is SkinnedMeshRenderer))
                list.Add(go);
        }

        var units = new HashSet<GameObject>(UnitPrefabs());
        return list.Where(p => p != null && !units.Contains(p)).Distinct().ToList();
    }

    public static List<string> NonToon(GameObject prefab)
    {
        var lines = new List<string>();
        foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == null || (mats[i].shader.name != TOON_SHADER && !IsExempt(mats[i])))
                    lines.Add($"{prefab.name}/{r.name}[{i}] {(mats[i] == null ? "<null>" : mats[i].shader.name)}");
        }
        return lines;
    }

    [MenuItem("Tools/TD/Apply Toon To Units")]
    private static void ApplyToUnits()
    {
        var template = AssetDatabase.LoadAssetAtPath<Material>(TEMPLATE_PATH);
        int n = UnitPrefabs().Sum(p => Apply(p, template));
        int moved = UnitPrefabs().Sum(SetOutlineLayer);
        AssetDatabase.SaveAssets();
        Debug.Log($"[TDToonConverter] {n} material slots switched to toon, {moved} renderers moved to {OUTLINE_LAYER}");
    }

    [MenuItem("Tools/TD/Apply Toon To Map")]
    private static void ApplyToMap()
    {
        var template = AssetDatabase.LoadAssetAtPath<Material>(TEMPLATE_PATH);
        int n = MapPrefabs().Sum(p => Apply(p, template));
        AssetDatabase.SaveAssets();
        Debug.Log($"[TDToonConverter] {n} map material slots switched to toon");
    }

    private static T Load<T>() where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
