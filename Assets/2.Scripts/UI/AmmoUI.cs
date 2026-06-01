using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AmmoUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;

    [Header("Text")]
    [SerializeField] private TMP_Text curAmmoText;
    [SerializeField] private TMP_Text maxAmmoText;

    [Header("Ammo Icons")]
    [SerializeField] private Transform iconContainer;    // 아이콘들 담을 부모
    [SerializeField] private GameObject ammoIconPrefab;  // 탄약 모양 아이콘 프리팹
    [SerializeField] private int maxDisplayCount = 12;   // 화면에 표시할 최대 아이콘 수

    [Header("Colors")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color emptyColor  = new Color(1f, 1f, 1f, 0.15f);

    private List<Image> _icons = new List<Image>();
    private MISSILE_TYPE _lastType;
    private int _lastMaxAmmo = -1;

    private void Update()
    {
        if (player == null) return;

        MissileAmmoInfo info = player.missileAmmoList
            .Find(x => x.missileType == player.curMissileType);

        if (info == null) return;

        // 무기 변경 or 최대 탄약 변경 시 아이콘 재생성
        if (info.missileType != _lastType || info.maxAmmo != _lastMaxAmmo)
            RebuildIcons(info.maxAmmo);

        UpdateIcons(info.curAmmo);

        if (curAmmoText != null) curAmmoText.text = info.curAmmo.ToString();
        if (maxAmmoText != null) maxAmmoText.text  = $"/ {info.maxAmmo}";
    }

    private void RebuildIcons(int maxAmmo)
    {
        foreach (var icon in _icons)
            if (icon != null) Destroy(icon.gameObject);
        _icons.Clear();

        // maxDisplayCount 초과 시 축소 표시
        int displayCount = Mathf.Min(maxAmmo, maxDisplayCount);

        for (int i = 0; i < displayCount; i++)
        {
            GameObject go = Instantiate(ammoIconPrefab, iconContainer);
            _icons.Add(go.GetComponent<Image>());
        }

        _lastMaxAmmo = maxAmmo;
        _lastType    = player.curMissileType;
    }

    private void UpdateIcons(int curAmmo)
    {
        // 실제 탄약 비율로 활성 아이콘 수 계산
        int activeCount = _lastMaxAmmo > 0
            ? Mathf.RoundToInt((float)curAmmo / _lastMaxAmmo * _icons.Count)
            : 0;

        for (int i = 0; i < _icons.Count; i++)
        {
            if (_icons[i] == null) continue;
            _icons[i].color = i < activeCount ? activeColor : emptyColor;
        }
    }
}