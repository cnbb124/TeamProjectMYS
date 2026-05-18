using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI;

public class StatPanelUI : MonoBehaviour
{
    [Header("Identity")]
    public Text unitNameText;
    public Text descriptionText;

    [Header("Stat Bars (Image.Type = Filled, Horizontal)")]
    public Image speedBar;
    public Image mobilityBar;
    public Image stabilityBar;
    public Image airToAirBar;
    public Image airToGroundBar;
    public Image defenseBar;

    [Header("Parts Slots")]
    public Text bodySlotText;
    public Text armSlotText;
    public Text miscSlotText;

    [Header("Weapons Ammo")]
    public Text gunAmmoText;
    public Text mslAmmoText;
    public Text flrAmmoText;
    public Text emplAmmoText;

    [Header("Animation")]
    public float animDuration = 0.35f;

    private Coroutine _anim;

    public void Refresh(PlaneNodeData data)
    {
        if (!data) return;

        if (unitNameText)   unitNameText.text   = $"[ {data.unitName.ToUpper()} ]";
        if (descriptionText) descriptionText.text = data.description;

        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateBars(data));

        SetSlotText(bodySlotText, data.usedBodySlots, data.bodySlots);
        SetSlotText(armSlotText,  data.usedArmSlots,  data.armSlots);
        SetSlotText(miscSlotText, data.usedMiscSlots, data.miscSlots);

        SetAmmoText(gunAmmoText,  data.gunAmmo);
        SetAmmoText(mslAmmoText,  data.mslAmmo);
        SetAmmoText(flrAmmoText,  data.flrAmmo);
        SetAmmoText(emplAmmoText, data.emplAmmo);
    }

    IEnumerator AnimateBars(PlaneNodeData data)
    {
        Image[] bars    = { speedBar, mobilityBar, stabilityBar, airToAirBar, airToGroundBar, defenseBar };
        float[] targets = { data.speed / 100f, data.mobility / 100f, data.stability / 100f,
                            data.airToAir / 100f, data.airToGround / 100f, data.defense / 100f };
        float[] starts  = new float[bars.Length];

        for (int i = 0; i < bars.Length; i++)
            starts[i] = bars[i] ? bars[i].fillAmount : 0f;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
            for (int i = 0; i < bars.Length; i++)
                if (bars[i]) bars[i].fillAmount = Mathf.Lerp(starts[i], targets[i], t);
            yield return null;
        }
        for (int i = 0; i < bars.Length; i++)
            if (bars[i]) bars[i].fillAmount = targets[i];
    }

    static void SetSlotText(Text tmp, int used, int total)
    {
        if (tmp) tmp.text = $"{used:D2} / {total:D2}";
    }
    static void SetAmmoText(Text tmp, int val)
    {
        if (tmp) tmp.text = val > 0 ? val.ToString() : "--";
    }
}