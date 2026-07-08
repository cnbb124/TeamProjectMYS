using UnityEngine;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
	private void Start()
	{
        SoundManager.Instance.PlayBGM(SOUND_TYPE.BGM_LOBBY);
	}

	public void OnClickPlay()
    {
        GameManager.Instance.LoadScene("ServerListUI");
    }

    public void OnClickNewGame()
    {
        //LoadingManager.NextScene = "Test";
        LoadingManager.NextScene = "STAGE1";
        GameManager.Instance.LoadScene(SCENE_TYPE.LOADING_SEQUENCE);
        //GameManager.Instance.LoadScene("Jayeon");

	}

    public void OnClickCharacter()
    {
        GameManager.Instance.LoadScene("CharacterScene");
    }

    public void OnClickShop()
    {
        GameManager.Instance.LoadScene("ShopScene");
    }

    public void OnClickSettings()
    {
        GameManager.Instance.LoadScene("SettingsScene");
    }

    public void OnClickQuickMatch()
    {
        GameManager.Instance.LoadScene("ServerListUI");
    }

}
