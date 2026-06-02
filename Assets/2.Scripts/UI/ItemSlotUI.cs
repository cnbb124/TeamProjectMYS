using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemSlotUI : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public Image        background;   // ?�롯 배경
        public Image        weaponIcon;   // Weapon_0X Image
        public GameObject   indicator;    // ItemIndicator (?�성??비활?�화)
        public TMP_Text     numberText;   // ?�롯 번호
        public Sprite       weaponSprite; // ?�당??무기 ?�이�?
    }

    [SerializeField] private Slot[] slots = new Slot[4];
    [SerializeField] private Player player;

    [Header("Colors")]
    [SerializeField] private Color activeColor   = Color.white;
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.4f);

    private int _currentSlot = -1;

    private readonly KeyCode[] _keys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2,
        KeyCode.Alpha3, KeyCode.Alpha4
    };

    private void Start()
    {
        InitSlots();
        SelectSlot(0);
    }

    private void Update()
    {
        for (int i = 0; i < _keys.Length; i++)
        {
            if (Input.GetKeyDown(_keys[i]))
                SelectSlot(i);
        }
    }

    private void InitSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].numberText != null)
                slots[i].numberText.text = (i + 1).ToString();

            if (slots[i].weaponIcon != null && slots[i].weaponSprite != null)
                slots[i].weaponIcon.sprite = slots[i].weaponSprite;
        }
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return;
        if (_currentSlot == index) return;

        _currentSlot = index;

        for (int i = 0; i < slots.Length; i++)
        {
            bool isActive = (i == _currentSlot);

            if (slots[i].indicator  != null)
                slots[i].indicator.SetActive(isActive);

            if (slots[i].background != null)
                slots[i].background.color = isActive ? activeColor : inactiveColor;
        }

        // 플레이어 미사일 슬롯 전환 (슬롯 0~ = missileSlots 인덱스)
        if (player != null && player.weaponSystem.missileSlots != null
            && index < player.weaponSystem.missileSlots.Count)
        {
            player.weaponSystem.SwitchToSlot(index);
        }
    }
}
