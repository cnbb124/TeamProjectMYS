using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라 모드에 따른 로컬 기체의 근거리 비주얼 표시만 담당합니다.
/// </summary>
public sealed class CockpitVisibilityController : MonoBehaviour
{
    [SerializeField] private GameObject _cockpitVisualRoot;
    [SerializeField] private bool _hideCockpitInThirdPerson;

    private Player _cachedPlayer;
    private readonly List<Renderer> _reverseThrusterRenderers = new List<Renderer>();
    private readonly HashSet<Renderer> _uniqueRenderers = new HashSet<Renderer>();
    private readonly List<Transform> _transformBuffer = new List<Transform>();
    private readonly List<Renderer> _rendererBuffer = new List<Renderer>();
    private bool _cockpitView;
    private float _nextRefreshTime;

    public void ApplyView(bool cockpitView)
    {
        _cockpitView = cockpitView;
        if (_cockpitVisualRoot != null && _hideCockpitInThirdPerson)
        {
            _cockpitVisualRoot.SetActive(cockpitView);
        }

        Player localPlayer = GameManager.Instance != null
            ? GameManager.Instance.playerRef
            : null;

        if (_cachedPlayer != localPlayer)
        {
            CacheReverseThrusters(localPlayer);
        }

        SetReverseThrustersVisible(!cockpitView);
    }

    private void Update()
    {
        if (!_cockpitView || Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + 1f;
        Player localPlayer = GameManager.Instance != null
            ? GameManager.Instance.playerRef
            : null;
        CacheReverseThrusters(localPlayer);
        SetReverseThrustersVisible(false);
    }

    private void CacheReverseThrusters(Player player)
    {
        _cachedPlayer = player;
        _reverseThrusterRenderers.Clear();
        _uniqueRenderers.Clear();
        _transformBuffer.Clear();
        _rendererBuffer.Clear();
        if (player == null)
        {
            return;
        }

        player.GetComponentsInChildren(true, _transformBuffer);
        for (int i = 0; i < _transformBuffer.Count; i++)
        {
            Transform target = _transformBuffer[i];
            string objectName = target.name;
            bool isReverseRoot =
                objectName.Equals("Thruster_Rev", StringComparison.OrdinalIgnoreCase) ||
                objectName.StartsWith("Rev-Booster", StringComparison.OrdinalIgnoreCase) ||
                objectName.StartsWith("Rev-Sub-Booster", StringComparison.OrdinalIgnoreCase);

            if (!isReverseRoot)
            {
                continue;
            }

            _rendererBuffer.Clear();
            target.GetComponentsInChildren(true, _rendererBuffer);
            for (int rendererIndex = 0; rendererIndex < _rendererBuffer.Count; rendererIndex++)
            {
                Renderer renderer = _rendererBuffer[rendererIndex];
                if (renderer != null && _uniqueRenderers.Add(renderer))
                {
                    _reverseThrusterRenderers.Add(renderer);
                }
            }
        }
    }

    private void SetReverseThrustersVisible(bool visible)
    {
        for (int i = 0; i < _reverseThrusterRenderers.Count; i++)
        {
            Renderer renderer = _reverseThrusterRenderers[i];
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }
}
