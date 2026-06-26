using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemSlotUI : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public Image        background;   // 슬롯 배경
        public Image        weaponIcon;   // Weapon_0X Image
        public GameObject   indicator;    // 선택 표시 오브젝트 (선택된 슬롯만 활성화)
        public TMP_Text     numberText;   // 슬롯 번호
        public Sprite       weaponSprite; // 해당 무기 이미지
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
    }
}
