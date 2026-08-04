using UnityEngine;

public static class OfflineSession
{
    public static bool IsActive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        IsActive = false;
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
}
