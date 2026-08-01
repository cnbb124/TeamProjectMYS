#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using Unity.XR.CoreUtils;

public static class SetupCockpitThrottleInteraction
{
    private const string PrefabPath = "Assets/3.Prefabs/cockpit/cockpit.prefab";
    private const string StarterOriginPath =
        "Assets/Samples/XR Interaction Toolkit/2.6.5/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    private const string InputActionsPath =
        "Assets/Samples/XR Interaction Toolkit/2.6.5/Starter Assets/XRI Default Input Actions.inputactions";
    private const string MenuRigPrefabPath =
        "Assets/Resources/XRMenuControllers.prefab";

    [MenuItem("Tools/VR/Setup Cockpit Throttle Interaction")]
    public static void Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        GameObject starterOrigin = null;

        try
        {
            Transform colliderTransform = FindDeepChild(root.transform, "Throttle_Handle_Collider");
            if (colliderTransform == null)
            {
                throw new MissingReferenceException("Throttle_Handle_Collider를 찾을 수 없습니다.");
            }

            BoxCollider box = colliderTransform.GetComponent<BoxCollider>();
            if (box == null)
            {
                throw new MissingComponentException("Throttle_Handle_Collider에 BoxCollider가 없습니다.");
            }

            box.isTrigger = true;

            CockpitThrottleInteractable legacyThrottle =
                colliderTransform.GetComponent<CockpitThrottleInteractable>();
            if (legacyThrottle != null)
            {
                legacyThrottle.enabled = false;
            }

            CockpitGripControls gripControls = root.GetComponent<CockpitGripControls>();
            if (gripControls == null)
            {
                gripControls = root.AddComponent<CockpitGripControls>();
            }

            SerializedObject serializedGripControls = new SerializedObject(gripControls);
            serializedGripControls.FindProperty("_throttleTravelMetres").floatValue = 0.12f;
            serializedGripControls.ApplyModifiedPropertiesWithoutUndo();

            CockpitViewSwitcher viewSwitcher = root.GetComponent<CockpitViewSwitcher>();
            if (viewSwitcher != null)
            {
                SerializedObject serializedViewSwitcher = new SerializedObject(viewSwitcher);
                serializedViewSwitcher.FindProperty("_createThirdPersonCameraIfMissing")
                    .boolValue = true;
                serializedViewSwitcher.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform xrOrigin = FindDeepChild(root.transform, "XR Origin");
            Transform cameraOffset = xrOrigin != null
                ? FindDirectChild(xrOrigin, "Camera Offset")
                : null;

            if (xrOrigin == null || cameraOffset == null)
            {
                throw new MissingReferenceException("콕핏의 XR Origin/Camera Offset을 찾을 수 없습니다.");
            }

            starterOrigin = PrefabUtility.LoadPrefabContents(StarterOriginPath);
            Transform starterCameraOffset = FindDirectChild(starterOrigin.transform, "Camera Offset");
            if (starterCameraOffset == null)
            {
                throw new MissingReferenceException("Starter Assets XR Origin의 Camera Offset을 찾을 수 없습니다.");
            }

            CopyControllerIfMissing(
                starterCameraOffset, cameraOffset, "Left Controller", hideVisual: true);
            CopyControllerIfMissing(
                starterCameraOffset, cameraOffset, "Right Controller", hideVisual: true);
            RemoveInteractionManager(xrOrigin.gameObject);
            EnsureInputActions(xrOrigin.gameObject);
            CreateMenuControllersPrefab(starterCameraOffset);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[SetupCockpitThrottleInteraction] 스로틀 및 좌우 XR 컨트롤러 연결 완료.");
        }
        finally
        {
            if (starterOrigin != null)
            {
                PrefabUtility.UnloadPrefabContents(starterOrigin);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CopyControllerIfMissing(
        Transform sourceParent,
        Transform destinationParent,
        string controllerName,
        bool hideVisual)
    {
        Transform existing = FindDirectChild(destinationParent, controllerName);
        if (existing != null)
        {
            DisableAutomaticModeManager(existing.gameObject);
            SetControllerVisualActive(existing, controllerName, !hideVisual);
            return;
        }

        Transform source = FindDirectChild(sourceParent, controllerName);
        if (source == null)
        {
            throw new MissingReferenceException($"Starter Assets에서 {controllerName}를 찾을 수 없습니다.");
        }

        GameObject controller = UnityEngine.Object.Instantiate(source.gameObject, destinationParent);
        controller.name = controllerName;
        controller.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        controller.transform.localScale = Vector3.one;
        DisableAutomaticModeManager(controller);

        // 콕핏에는 고정 파일럿 손이 이미 표시되므로 샘플 컨트롤러 모델은 숨긴다.
        SetControllerVisualActive(controller.transform, controllerName, !hideVisual);
    }

    private static void SetControllerVisualActive(
        Transform controller,
        string controllerName,
        bool active)
    {
        string hand = controllerName.Split(' ')[0];
        Transform visual = FindDeepChild(controller, $"XR Controller {hand}");
        if (visual != null)
        {
            visual.gameObject.SetActive(active);
        }
    }

    private static void CreateMenuControllersPrefab(Transform starterCameraOffset)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        GameObject menuRig = new GameObject("XRMenuControllers");
        try
        {
            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(menuRig.transform, false);

            GameObject trackingCameraObject = new GameObject("Tracking Camera");
            trackingCameraObject.transform.SetParent(cameraOffset.transform, false);
            Camera trackingCamera = trackingCameraObject.AddComponent<Camera>();
            trackingCamera.enabled = false;
            trackingCameraObject.SetActive(false);

            XROrigin origin = menuRig.AddComponent<XROrigin>();
            SerializedObject serializedOrigin = new SerializedObject(origin);
            serializedOrigin.FindProperty("m_CameraFloorOffsetObject").objectReferenceValue =
                cameraOffset;
            serializedOrigin.ApplyModifiedPropertiesWithoutUndo();

            CopyControllerIfMissing(
                starterCameraOffset, cameraOffset.transform, "Left Controller", hideVisual: false);
            CopyControllerIfMissing(
                starterCameraOffset, cameraOffset.transform, "Right Controller", hideVisual: false);
            EnsureInteractionServices(menuRig);
            SetMenuInteractorDefaults(menuRig);
            PrefabUtility.SaveAsPrefabAsset(menuRig, MenuRigPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(menuRig);
        }
    }

    private static void DisableAutomaticModeManager(GameObject controller)
    {
        MonoBehaviour[] behaviours = controller.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null &&
                behaviour.GetType().Name == "ActionBasedControllerManager")
            {
                behaviour.enabled = false;
            }
        }
    }

    private static void SetMenuInteractorDefaults(GameObject menuRig)
    {
        XRDirectInteractor[] direct =
            menuRig.GetComponentsInChildren<XRDirectInteractor>(true);
        for (int i = 0; i < direct.Length; i++)
        {
            direct[i].gameObject.SetActive(false);
        }

        XRPokeInteractor[] poke =
            menuRig.GetComponentsInChildren<XRPokeInteractor>(true);
        for (int i = 0; i < poke.Length; i++)
        {
            poke[i].gameObject.SetActive(false);
        }

        XRRayInteractor[] rays =
            menuRig.GetComponentsInChildren<XRRayInteractor>(true);
        for (int i = 0; i < rays.Length; i++)
        {
            rays[i].gameObject.SetActive(rays[i].name == "Ray Interactor");
        }
    }

    private static void EnsureInteractionServices(GameObject xrOrigin)
    {
        if (xrOrigin.GetComponent<XRInteractionManager>() == null)
        {
            xrOrigin.AddComponent<XRInteractionManager>();
        }

        EnsureInputActions(xrOrigin);
    }

    private static void EnsureInputActions(GameObject xrOrigin)
    {
        InputActionManager inputManager = xrOrigin.GetComponent<InputActionManager>();
        if (inputManager == null)
        {
            inputManager = xrOrigin.AddComponent<InputActionManager>();
        }

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (actions == null)
        {
            throw new MissingReferenceException("XRI Default Input Actions를 찾을 수 없습니다.");
        }

        SerializedObject serializedManager = new SerializedObject(inputManager);
        SerializedProperty assets = serializedManager.FindProperty("m_ActionAssets");
        if (assets == null)
        {
            throw new MissingMemberException("InputActionManager.m_ActionAssets를 찾을 수 없습니다.");
        }

        assets.arraySize = 1;
        assets.GetArrayElementAtIndex(0).objectReferenceValue = actions;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RemoveInteractionManager(GameObject xrOrigin)
    {
        XRInteractionManager manager = xrOrigin.GetComponent<XRInteractionManager>();
        if (manager != null)
        {
            UnityEngine.Object.DestroyImmediate(manager);
        }
    }

    private static Transform FindDirectChild(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
            {
                return child;
            }

            Transform result = FindDeepChild(child, targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
#endif
