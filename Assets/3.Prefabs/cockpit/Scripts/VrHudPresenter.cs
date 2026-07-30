using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VR HUD를 원본 XR 카메라의 양안 렌더 패스에 포함하고,
/// 시점 종료 시 HUD 레이어와 카메라 마스크를 원래 상태로 복구합니다.
/// </summary>
public sealed class VrHudPresenter : MonoBehaviour
{
    private const string HudLayerName = "VRHUD";

    private readonly Dictionary<Transform, int> _originalLayers =
        new Dictionary<Transform, int>();
    private readonly List<Transform> _restoreBuffer = new List<Transform>();

    private Camera _sourceCamera;
    private int _hudLayerMask;
    private bool _sourceIncludedHudLayer;

    public bool Show(Camera sourceCamera, Canvas canvas)
    {
        if (sourceCamera == null || canvas == null)
        {
            return false;
        }

        int hudLayer = LayerMask.NameToLayer(HudLayerName);
        if (hudLayer < 0)
        {
            Debug.LogError(
                $"[{nameof(VrHudPresenter)}] {HudLayerName} Layer가 없습니다.",
                this);
            return false;
        }

        RemoveDestroyedLayerEntries();
        EnsureSourceCameraRendersHud(sourceCamera, hudLayer);
        SetLayerRecursively(canvas.transform, hudLayer);
        return true;
    }

    public void Restore(Canvas canvas)
    {
        _restoreBuffer.Clear();
        Transform canvasRoot = canvas != null ? canvas.transform : null;

        foreach (KeyValuePair<Transform, int> entry in _originalLayers)
        {
            Transform target = entry.Key;
            if (target == null ||
                canvasRoot == null ||
                target == canvasRoot ||
                target.IsChildOf(canvasRoot))
            {
                _restoreBuffer.Add(target);
            }
        }

        for (int i = 0; i < _restoreBuffer.Count; i++)
        {
            Transform target = _restoreBuffer[i];
            if (target != null &&
                _originalLayers.TryGetValue(target, out int originalLayer))
            {
                target.gameObject.layer = originalLayer;
            }

            _originalLayers.Remove(target);
        }

        FinishRestoreIfEmpty();
    }

    public void RestoreAll()
    {
        foreach (KeyValuePair<Transform, int> entry in _originalLayers)
        {
            if (entry.Key != null)
            {
                entry.Key.gameObject.layer = entry.Value;
            }
        }

        _originalLayers.Clear();
        FinishRestoreIfEmpty();
    }

    private void EnsureSourceCameraRendersHud(
        Camera sourceCamera,
        int hudLayer)
    {
        if (_sourceCamera != sourceCamera)
        {
            RestoreSourceCameraLayer();
            _sourceCamera = sourceCamera;
            _hudLayerMask = 1 << hudLayer;
            _sourceIncludedHudLayer =
                (sourceCamera.cullingMask & _hudLayerMask) != 0;
        }

        // World-space UI is rendered by the same XR camera and render pass as
        // the cockpit. This keeps both eyes in sync in Single Pass Instanced.
        sourceCamera.cullingMask |= _hudLayerMask;
    }

    private void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        if (!_originalLayers.ContainsKey(root))
        {
            _originalLayers.Add(root, root.gameObject.layer);
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private void RemoveDestroyedLayerEntries()
    {
        _restoreBuffer.Clear();
        foreach (KeyValuePair<Transform, int> entry in _originalLayers)
        {
            if (entry.Key == null)
            {
                _restoreBuffer.Add(entry.Key);
            }
        }

        for (int i = 0; i < _restoreBuffer.Count; i++)
        {
            _originalLayers.Remove(_restoreBuffer[i]);
        }
    }

    private void FinishRestoreIfEmpty()
    {
        if (_originalLayers.Count != 0)
        {
            return;
        }

        RestoreSourceCameraLayer();
    }

    private void RestoreSourceCameraLayer()
    {
        if (_sourceCamera == null)
        {
            return;
        }

        if (_sourceIncludedHudLayer)
        {
            _sourceCamera.cullingMask |= _hudLayerMask;
        }
        else
        {
            _sourceCamera.cullingMask &= ~_hudLayerMask;
        }

        _sourceCamera = null;
        _hudLayerMask = 0;
        _sourceIncludedHudLayer = false;
    }

    private void OnDestroy()
    {
        RestoreAll();
    }
}
