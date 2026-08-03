using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class XRMenuSurfaceSetup
{
    private static readonly string[] ScenePaths =
    {
        "Assets/1.Scenes/BuildScene/MAIN.unity",
        "Assets/1.Scenes/BuildScene/BASE_HANGAR.unity"
    };

    public static void Inspect()
    {
        for (int sceneIndex = 0; sceneIndex < ScenePaths.Length; sceneIndex++)
        {
            string scenePath = ScenePaths[sceneIndex];
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            Debug.Log($"[XRMenuSurfaceSetup] Scene={scene.name}");

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.gameObject.scene != scene || !canvas.isRootCanvas)
                {
                    continue;
                }

                int selectableCount = canvas.GetComponentsInChildren<Selectable>(true).Length;
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(canvas.gameObject);
                Debug.Log(
                    $"[XRMenuSurfaceSetup] Candidate={GetPath(canvas.transform)} " +
                    $"active={canvas.gameObject.activeInHierarchy} " +
                    $"renderMode={canvas.renderMode} selectables={selectableCount} " +
                    $"prefab={prefabPath}");
            }
        }
    }

    [MenuItem("Tools/VR/Setup Explicit Menu Surfaces")]
    public static void Apply()
    {
        Scene originalScene = SceneManager.GetActiveScene();
        string originalPath = originalScene.path;

        for (int sceneIndex = 0; sceneIndex < ScenePaths.Length; sceneIndex++)
        {
            string scenePath = ScenePaths[sceneIndex];
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Canvas selected = FindUnambiguousMenuCanvas(scene, out string reason);
            if (selected == null)
            {
                Debug.LogError($"[XRMenuSurfaceSetup] {scene.name}: {reason}. Scene was not saved.");
                continue;
            }

            XRMenuSurface[] existing = Resources.FindObjectsOfTypeAll<XRMenuSurface>();
            for (int i = 0; i < existing.Length; i++)
            {
                XRMenuSurface marker = existing[i];
                if (marker != null && marker.gameObject.scene == scene && marker.gameObject != selected.gameObject)
                {
                    Object.DestroyImmediate(marker);
                }
            }

            if (selected.GetComponent<XRMenuSurface>() == null)
            {
                selected.gameObject.AddComponent<XRMenuSurface>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[XRMenuSurfaceSetup] {scene.name}: linked {GetPath(selected.transform)}");
        }

        if (!string.IsNullOrEmpty(originalPath))
        {
            EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
        }
    }

    private static Canvas FindUnambiguousMenuCanvas(Scene scene, out string reason)
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        Canvas best = null;
        int bestCount = 0;
        bool tied = false;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.gameObject.scene != scene || !canvas.isRootCanvas)
            {
                continue;
            }

            int count = canvas.GetComponentsInChildren<Selectable>(true).Length;
            if (count > bestCount)
            {
                best = canvas;
                bestCount = count;
                tied = false;
            }
            else if (count > 0 && count == bestCount)
            {
                tied = true;
            }
        }

        if (best == null)
        {
            reason = "no root Canvas with Selectables was found";
            return null;
        }

        if (tied)
        {
            reason = $"multiple root Canvases contain {bestCount} Selectables";
            return null;
        }

        reason = null;
        return best;
    }

    private static string GetPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }
}
