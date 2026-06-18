using System.Collections.Generic;
using UnityEngine;

// 모든 ItemData SO를 id로 조회하는 데이터베이스.
// 에디터 인스펙터의 "전체 스캔 & 갱신" 버튼으로 allItems 배열을 채운 뒤 커밋.
// GameManager.Awake()에서 itemDatabase.Init() 호출 필수.
// 조회: ItemDatabase.Instance.Get(ITEM_ID.FRAME_DEFAULT)
//        ItemDatabase.Instance.Get<PartData>(ITEM_ID.FRAME_DEFAULT)
[CreateAssetMenu(fileName= "New Item Database",menuName = "Create Data/Item/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public static ItemDatabase Instance { get; private set; }

    public ItemData[] allItems;

    private Dictionary<int, ItemData> _db = new Dictionary<int, ItemData>();

    public void Init()
    {
        Instance = this;
        _db.Clear();
        foreach (ItemData item in allItems)
        {
            if (item == null || item.id == ITEM_ID.NONE)
            {
                continue;
            }
            _db[(int)item.id] = item;
        }
    }

    public ItemData Get(ITEM_ID id)
    {
        ItemData result;
        _db.TryGetValue((int)id, out result);
        return result;
    }

    public T Get<T>(ITEM_ID id) where T : ItemData
    {
        return Get(id) as T;
    }

    public ItemData Get(int id)
    {
        ItemData result;
        _db.TryGetValue(id, out result);
        return result;
    }
}
