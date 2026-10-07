using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Red square on the ground under a melee whose block slots are all taken (spec §5.2): the
/// warning BEFORE the next enemy leaks, not after. Built in code and attached at runtime, the same
/// way as TDMoraleIconView — no prefab edits.
/// </summary>
public sealed class TDBlockFullMarker : MonoBehaviour
{
    private const float GROUND_LIFT = 0.02f;
    private const float CELL_FRACTION = 0.9f;
    private const string COLOR = "#FB2425";
    private const float ALPHA = 0.6f;

    private static Material s_Material;

    public static TDBlockFullMarker Attach(Transform owner)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(go.GetComponent<Collider>()); // must never catch the tap meant for the operator
        go.name = "BlockFullMarker";
        go.transform.SetParent(owner, worldPositionStays: false);

        var marker = go.AddComponent<TDBlockFullMarker>();
        marker.Build(owner);
        return marker;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    private void Build(Transform owner)
    {
        float world = TDConstant.CONFIG_GRID_CELL_SIZE * CELL_FRACTION;
        transform.localScale = Vector3.one * (world / Mathf.Max(0.0001f, owner.lossyScale.x));
        GetComponent<MeshRenderer>().sharedMaterial = SharedMaterial();
        GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        gameObject.SetActive(false);
    }

    // ponytail: transparent URP Unlit set up by hand. If a build strips the transparent variant the
    // marker renders opaque red — still a warning; ship a material asset if that ever shows up.
    private static Material SharedMaterial()
    {
        if (s_Material != null) return s_Material;

        ColorUtility.TryParseHtmlString(COLOR, out var c);
        c.a = ALPHA;
        s_Material = new Material(Shader.Find(TDConstant.SHADER_URP_UNLIT)) { color = c, hideFlags = HideFlags.DontSave };
        s_Material.SetFloat("_Surface", 1f);
        s_Material.SetFloat("_ZWrite", 0f);
        s_Material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        s_Material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        s_Material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        s_Material.renderQueue = (int)RenderQueue.Transparent;
        return s_Material;
    }

    private void LateUpdate()
    {
        // Flat on the road whichever way the operator has turned to face its target. Measured from
        // the path tile's top face, not the operator's feet: the root sits at 0.02, under the 0.1
        // tile, so "feet + lift" buried the marker inside the road.
        var p = transform.parent.position;
        transform.SetPositionAndRotation(new Vector3(p.x, TDConstant.CONFIG_PATH_OFFSET_Y + GROUND_LIFT, p.z),
                                         Quaternion.Euler(90f, 0f, 0f));
    }
}
