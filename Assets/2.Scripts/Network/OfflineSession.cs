using System.Text;
using UnityEngine;

public static class OfflineSession
{
    public const string GuestKey = "guest";

    public static bool IsActive { get; private set; }

    /// <summary>로그인 화면에서 입력한 아이디. 로컬 세이브 파일을 가르는 기준이며 서버 접속 여부와 무관함.</summary>
    public static string AccountId { get; private set; } = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        IsActive = false;
        AccountId = "";
    }

    public static void Enter()
    {
        IsActive = true;
        Debug.Log("[OfflineSession] 오프라인 모드로 시작합니다. 로컬 저장만 사용합니다.");
    }

    public static void Exit()
    {
        IsActive = false;
    }

    public static void SetAccount(string id)
    {
        AccountId = string.IsNullOrWhiteSpace(id) ? "" : id.Trim();
    }

    /// <summary>파일명에 넣을 수 있게 정리한 키. 아이디가 없으면 게스트 칸을 씀.</summary>
    public static string AccountFileKey
    {
        get
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                return GuestKey;
            }

            StringBuilder builder = new StringBuilder(AccountId.Length);
            foreach (char c in AccountId)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('_');
                }
            }
            return builder.Length > 0 ? builder.ToString() : GuestKey;
        }
    }
}
