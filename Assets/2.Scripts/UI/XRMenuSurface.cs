using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Explicitly marks the one scene-owned Canvas used as an XR menu surface.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(GraphicRaycaster))]
public sealed class XRMenuSurface : MonoBehaviour
{
    public Canvas Canvas => GetComponent<Canvas>();
    public GraphicRaycaster Raycaster => GetComponent<GraphicRaycaster>();
}
