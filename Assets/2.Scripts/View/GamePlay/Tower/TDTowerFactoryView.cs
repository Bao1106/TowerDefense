using UnityEngine;

/// <summary>
/// Subscriber của TDTowerFactoryControl.onCreateTowerSuccess — backward-compat.
/// Init(key) giờ được gọi bên trong TDTowerFactoryControl.CreateUnit qua IPlacedUnit.
/// File này giữ lại để không break scene reference; không còn logic thực.
/// </summary>
public class TDTowerFactoryView : MonoBehaviour
{
}
