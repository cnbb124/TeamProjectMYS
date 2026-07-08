using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonConnectionTest : MonoBehaviourPunCallbacks
{
    void Start()
    {
        Debug.Log("Photon 서버 연결 시도 중...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("✅ Photon 마스터 서버 연결 성공!");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError("❌ 연결 실패: " + cause);
    }
}