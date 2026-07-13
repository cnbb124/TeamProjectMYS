/*
 * [MYSNetworkSetupMenu]
 * 로그인/채팅을 원클릭으로 세팅해주는 에디터 메뉴.
 *
 *   Tools > MYS > 로그인 연결 셋업 : 로그인 씬(TestLoginScene)에 LoginUI 부착 + 참조 연결 + ServerApi 배치
 *   Tools > MYS > 채팅 셋업        : 한글 폰트(SUITE) 등록 + 로비 Canvas 프리팹의 채팅창을 ChatUI/포톤에 연결
 *
 * 실행 후 씬 저장(Ctrl+S) 필수!
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class MYSNetworkSetupMenu
{
    // =====================================================================
    // 메뉴 1: 로그인 연결 셋업 (TestLoginScene 열어놓고 클릭)
    // =====================================================================
    [MenuItem("Tools/MYS/로그인 연결 셋업")]
    public static void SetupLogin()
    {
        // (1) UI 요소들 이름으로 찾기 (우석 씬 구조 기준)
        TMP_InputField idField = FindByName<TMP_InputField>("ID_Field");
        TMP_InputField pwField = FindByName<TMP_InputField>("PW_Field");
        Button         button  = FindByName<Button>("Button");

        if (idField == null || pwField == null || button == null)
        {
            EditorUtility.DisplayDialog("로그인 셋업",
                "ID_Field / PW_Field / Button 을 씬에서 못 찾았습니다.\n로그인 씬(TestLoginScene_Wooseok)을 열고 다시 실행하세요.", "확인");
            return;
        }

        // (2) LoginUI 부착할 자리: "LoginUI" 오브젝트 있으면 거기, 없으면 새로 만듦
        GameObject host = GameObject.Find("LoginUI");
        if (host == null) host = new GameObject("LoginUI");
        LoginUI loginUI = host.GetComponent<LoginUI>();
        if (loginUI == null) loginUI = Undo.AddComponent<LoginUI>(host);

        // (3) 참조 연결 (SerializedObject로 private 필드에 주입)
        var so = new SerializedObject(loginUI);
        so.FindProperty("idField").objectReferenceValue     = idField;
        so.FindProperty("pwField").objectReferenceValue     = pwField;
        so.FindProperty("loginButton").objectReferenceValue = button;

        // (4) 상태 표시 텍스트 없으면 만들어줌 (버튼 아래)
        TMP_Text status = FindByName<TMP_Text>("StatusText");
        if (status == null)
        {
            var go = new GameObject("StatusText", typeof(RectTransform));
            go.transform.SetParent(button.transform.parent, false);
            status = go.AddComponent<TextMeshProUGUI>();
            status.fontSize = 20;
            status.alignment = TextAlignmentOptions.Center;
            status.text = "";
            var rt = go.GetComponent<RectTransform>();
            var brt = button.GetComponent<RectTransform>();
            rt.anchorMin = brt.anchorMin; rt.anchorMax = brt.anchorMax;
            rt.anchoredPosition = brt.anchoredPosition + new Vector2(0, -60);
            rt.sizeDelta = new Vector2(400, 40);
            Undo.RegisterCreatedObjectUndo(go, "StatusText 생성");
        }
        so.FindProperty("statusText").objectReferenceValue = status;
        so.ApplyModifiedProperties();

        // (5) ServerApi 프리팹이 씬에 없으면 배치
        if (Object.FindObjectOfType<ServerApi>(true) == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3.Prefabs/Network/ServerApi.prefab");
            if (prefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(inst, "ServerApi 배치");
            }
            else Debug.LogWarning("[MYS] ServerApi.prefab을 못 찾음 — 수동으로 씬에 배치하세요");
        }

        EditorUtility.SetDirty(loginUI);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("로그인 셋업 완료",
            "LoginUI 연결 + ServerApi 배치 완료!\n\n" +
            "· ServerApi의 Server Url 확인 (내 PC 테스트 = http://localhost:5080)\n" +
            "· 씬 저장(Ctrl+S) 후 플레이로 테스트", "확인");
    }

    // =====================================================================
    // 메뉴 2: 채팅 셋업 (한글 폰트 + 로비 채팅창 연결을 한 번에)
    //   씬 안 열어도 됨 — 로비 Canvas 프리팹을 직접 수정한다.
    // =====================================================================
    [MenuItem("Tools/MYS/채팅 셋업")]
    public static void SetupChat()
    {
        // (1) 한글 폰트 먼저 — 채팅 메시지 프리팹에 폰트가 입혀지도록 순서 중요
        TMP_FontAsset font = EnsureKoreanFont();

        // (2) 로비 채팅창(우석 Canvas.prefab)에 ChatUI + 포톤 연결
        if (!WireLobbyChat(out string chatSummary))
            return;   // 실패 원인은 WireLobbyChat 내부에서 이미 안내함

        EditorUtility.DisplayDialog("채팅 셋업 완료",
            "① 한글 폰트(SUITE): " + (font != null ? "등록 완료" : "실패 — SUITE-Regular.ttf 확인 필요") + "\n" +
            "② 로비 채팅 연결: 완료\n" + chatSummary + "\n\n" +
            "· 테스트로비 씬 플레이하면 로컬 에코 모드로 바로 동작\n" +
            "· 진짜 채팅: PhotonServerSettings의 App Id Chat 입력 필요\n" +
            "· 씬/프리팹 저장(Ctrl+S) 잊지 마세요", "확인");
    }

    // =====================================================================
    // 채팅 세부 1: 한글 폰트(SUITE) TMP 에셋 생성 + 전역 폴백 등록
    //   기본 폰트에 없는 글자(한글)를 자동으로 SUITE로 표시 → 게임 전체 네모(□) 해결.
    //   반환: 만들어진(또는 기존) 폰트 에셋. 실패 시 null.
    // =====================================================================
    private static TMP_FontAsset EnsureKoreanFont()
    {
        const string ttfPath   = "Assets/4.Art/Fonts/SUITE-Regular.ttf";
        const string assetPath = "Assets/4.Art/Fonts/SUITE-Regular SDF.asset";

        // (1) TMP 폰트 에셋 생성 (이미 있으면 재사용)
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fontAsset == null)
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (sourceFont == null)
            {
                Debug.LogError("[MYS] " + ttfPath + " 를 못 찾음 — 한글 폰트 건너뜀");
                return null;
            }

            // 다이나믹 모드 — 글자가 필요할 때 실시간으로 아틀라스에 추가 (채팅처럼 예측 불가한 입력에 적합)
            fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            fontAsset.name = "SUITE-Regular SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            fontAsset.material.name     = fontAsset.name + " Material";
            fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            AssetDatabase.SaveAssets();
        }

        // (2) TMP 전역 설정의 폴백 목록에 등록 (이미 있으면 건너뜀)
        var settings = TMP_Settings.instance;
        var soSet = new SerializedObject(settings);
        var listProp = soSet.FindProperty("m_fallbackFontAssets");
        bool already = false;
        for (int i = 0; i < listProp.arraySize; i++)
            if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset) { already = true; break; }
        if (!already)
        {
            listProp.arraySize++;
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = fontAsset;
            soSet.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // (3) 채팅 메시지 프리팹이 이미 있으면 SUITE를 기본 폰트로 (한/영 섞여도 모양 통일)
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3.Prefabs/UI/ChatMessage.prefab") != null)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents("Assets/3.Prefabs/UI/ChatMessage.prefab");
            var t = contents.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.font = fontAsset;
            PrefabUtility.SaveAsPrefabAsset(contents, "Assets/3.Prefabs/UI/ChatMessage.prefab");
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return fontAsset;
    }

    // =====================================================================
    // 채팅 세부 2: 로비 Canvas 프리팹의 기존 채팅창(우석 제작)에
    //   ChatUI + PhotonChatManager 부착 + 참조 연결. 프리팹을 직접 수정한다.
    //   반환: 성공 여부. summary에 버튼 연결 결과 등 요약.
    // =====================================================================
    private static bool WireLobbyChat(out string summary)
    {
        summary = "";
        const string canvasPath = "Assets/3.Prefabs/UI/Canvas.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(canvasPath);
        if (root == null)
        {
            EditorUtility.DisplayDialog("채팅 셋업", canvasPath + " 를 못 찾았습니다.", "확인");
            return false;
        }

        try
        {
            // (1) 프리팹 안에서 "ChatUI" 오브젝트(채팅창 틀) 찾기
            Transform chatRoot = FindDeep(root.transform, "ChatUI");
            if (chatRoot == null)
            {
                EditorUtility.DisplayDialog("채팅 셋업",
                    "Canvas.prefab 안에서 'ChatUI' 오브젝트를 못 찾았습니다.", "확인");
                return false;
            }

            // (2) 채팅창 부품들 찾기 (ChatUI 하위 → 없으면 프리팹 전체에서)
            ScrollRect scroll    = chatRoot.GetComponentInChildren<ScrollRect>(true);
            TMP_InputField input = chatRoot.GetComponentInChildren<TMP_InputField>(true);
            if (input == null) input = root.GetComponentInChildren<TMP_InputField>(true);

            Button enterBtn = null;
            foreach (var b in chatRoot.GetComponentsInChildren<Button>(true))
                if (b.name == "EnterButton") { enterBtn = b; break; }
            if (enterBtn == null)
                foreach (var b in root.GetComponentsInChildren<Button>(true))
                    if (b.name == "EnterButton") { enterBtn = b; break; }

            Transform content = scroll != null ? scroll.content : null;

            if (scroll == null || input == null || content == null)
            {
                EditorUtility.DisplayDialog("채팅 셋업",
                    "채팅창 부품이 부족합니다:\n" +
                    $"· Scroll View: {(scroll != null ? "OK" : "없음")}\n" +
                    $"· InputField(TMP): {(input != null ? "OK" : "없음")}\n" +
                    $"· Content: {(content != null ? "OK" : "없음")}", "확인");
                return false;
            }

            // (3) Content에 세로 쌓기 레이아웃 보장 (없으면 메시지가 안 쌓임)
            if (content.GetComponent<VerticalLayoutGroup>() == null)
            {
                var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 4, 4);
                vlg.spacing = 2;
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;  vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            }
            if (content.GetComponent<ContentSizeFitter>() == null)
            {
                var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // (4) ChatUI 스크립트 부착 + 참조 연결
            ChatUI chatUI = chatRoot.GetComponent<ChatUI>();
            if (chatUI == null) chatUI = chatRoot.gameObject.AddComponent<ChatUI>();

            var soChat = new SerializedObject(chatUI);
            soChat.FindProperty("scrollRect").objectReferenceValue    = scroll;
            soChat.FindProperty("content").objectReferenceValue       = content;
            soChat.FindProperty("inputField").objectReferenceValue    = input;
            soChat.FindProperty("sendButton").objectReferenceValue    = enterBtn;
            soChat.FindProperty("messagePrefab").objectReferenceValue = GetOrCreateMessagePrefab();
            soChat.ApplyModifiedProperties();

            // (5) PhotonChatManager 부착 + 연결
            PhotonChatManager mgr = chatRoot.GetComponent<PhotonChatManager>();
            if (mgr == null) mgr = chatRoot.gameObject.AddComponent<PhotonChatManager>();
            var soMgr = new SerializedObject(mgr);
            soMgr.FindProperty("chatUI").objectReferenceValue = chatUI;
            soMgr.ApplyModifiedProperties();

            // (6) 프리팹에 저장 — 이 프리팹을 쓰는 모든 씬(테스트로비 등)에 자동 반영
            PrefabUtility.SaveAsPrefabAsset(root, canvasPath);

            summary = $"   · 보내기 버튼(EnterButton): {(enterBtn != null ? "연결됨" : "못 찾음 — Enter키로만 전송")}";
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =====================================================================
    // 도우미들
    // =====================================================================

    /// <summary>메시지 한 줄 프리팹 (없으면 생성, 있으면 재사용).</summary>
    private static GameObject GetOrCreateMessagePrefab()
    {
        const string prefabDir  = "Assets/3.Prefabs/UI";
        const string prefabPath = prefabDir + "/ChatMessage.prefab";

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        var msgGo = new GameObject("ChatMessage", typeof(RectTransform));
        var msgText = msgGo.AddComponent<TextMeshProUGUI>();
        msgText.fontSize = 18;
        msgText.enableWordWrapping = true;
        msgText.text = "message";

        // 한글 폰트(SUITE SDF)가 만들어져 있으면 기본 적용
        var koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/4.Art/Fonts/SUITE-Regular SDF.asset");
        if (koreanFont != null) msgText.font = koreanFont;
        var le = msgGo.AddComponent<LayoutElement>();
        le.minHeight = 24;

        if (!AssetDatabase.IsValidFolder(prefabDir))
            AssetDatabase.CreateFolder("Assets/3.Prefabs", "UI");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(msgGo, prefabPath);
        Object.DestroyImmediate(msgGo);
        return prefab;
    }

    /// <summary>트리 전체에서 이름으로 Transform 찾기 (재귀).</summary>
    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform child in t)
        {
            Transform r = FindDeep(child, name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>씬에서 이름으로 컴포넌트 찾기 (비활성 포함).</summary>
    private static T FindByName<T>(string name) where T : Component
    {
        foreach (T c in Object.FindObjectsOfType<T>(true))
            if (c.gameObject.name == name) return c;
        return null;
    }
}
