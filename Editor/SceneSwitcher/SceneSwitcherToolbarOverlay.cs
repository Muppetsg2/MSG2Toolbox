// The #if is redundant inside an Editor-only assembly (see MSG2.Toolbox.Editor.asmdef), but it keeps the file safe
// if someone copies it into a non-Editor folder of their Assets.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MSG2.Toolbox.SceneSwitcher
{
    /// <summary>Scene list, settings and scene opening shared by all Scene Switcher toolbar elements.</summary>
    public static class EditorSceneSwitcher
    {
        // Stored per project and per user (UserSettings/EditorUserSettings.asset), so they survive domain reloads
        // and editor restarts instead of resetting to false after every compilation.
        private const string AutoEnterPlaymodeKey = "MSG2.SceneSwitcher.AutoEnterPlaymode";
        private const string ShowAllScenesKey = "MSG2.SceneSwitcher.ShowAllScenes";

        /// <summary>Raised when a setting changes, so toggles in other Scene Views stay in sync.</summary>
        public static event Action SettingsChanged;

        public static bool AutoEnterPlaymode
        {
            get => EditorUserSettings.GetConfigValue(AutoEnterPlaymodeKey) == "1";
            set => SetSetting(AutoEnterPlaymodeKey, value);
        }

        public static bool ShowAllScenes
        {
            get => EditorUserSettings.GetConfigValue(ShowAllScenesKey) == "1";
            set => SetSetting(ShowAllScenesKey, value);
        }

        private static void SetSetting(string key, bool value)
        {
            EditorUserSettings.SetConfigValue(key, value ? "1" : "0");
            SettingsChanged?.Invoke();
        }

        /// <summary>
        /// Builds the scene list on demand, so it is always up to date (new / renamed / deleted scenes and
        /// Build Settings changes) without listening to any project events.
        /// </summary>
        public static List<string> GetScenePaths()
        {
            var paths = new List<string>();

            if (ShowAllScenes)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));

                paths.Sort(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                foreach (var buildScene in EditorBuildSettings.scenes)
                {
                    // Build Settings can still list scenes that were deleted or moved outside Unity.
                    if (buildScene.enabled && AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path) != null)
                        paths.Add(buildScene.path);
                }
            }

            return paths;
        }

        public static void OpenScene(string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // The user pressed Cancel in the "save changes?" dialog: don't open the scene and don't enter Play Mode.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (AutoEnterPlaymode)
                EditorApplication.EnterPlaymode();
        }
    }

    [Icon("d_SceneAsset Icon")]
    [Overlay(typeof(SceneView), OverlayID, "Scene Switcher")]
    public class SceneSwitcherToolbarOverlay : ToolbarOverlay
    {
        public const string OverlayID = "msg2-scene-switcher-overlay";

        private SceneSwitcherToolbarOverlay() : base(
            SceneDropdown.ID,
            AutoEnterPlayModeToggle.ID,
            ShowAllScenesToggle.ID
        )
        {
        }
    }

    [EditorToolbarElement(ID, typeof(SceneView))]
    public class SceneDropdown : EditorToolbarDropdown
    {
        public const string ID = SceneSwitcherToolbarOverlay.OverlayID + "/scene-dropdown";

        private const string Tooltip = "Switch scene.";

        public SceneDropdown()
        {
            var content = EditorGUIUtility.TrTextContentWithIcon(string.Empty, Tooltip, "d_SceneAsset Icon");
            tooltip = content.tooltip;
            icon = content.image as Texture2D;

            // Look the label up by type instead of by child index, which depends on Unity's internal layout.
            var label = this.Q<TextElement>();
            if (label != null)
            {
                label.style.paddingLeft = 5;
                label.style.paddingRight = 5;
            }

            RefreshLabel();

            clicked += ShowMenu;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.projectChanged += RefreshLabel;                         // active scene renamed / moved
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;  // open, multi-scene, close
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;                  // File > New Scene
            EditorSceneManager.sceneSaved += OnSceneSaved;                            // "Untitled" saved as ...
            SceneManager.activeSceneChanged += OnActiveSceneChanged;                  // SceneManager.LoadScene in Play Mode

            // The element can be created while already in Play Mode (e.g. the overlay is shown during Play).
            SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            RefreshLabel();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.projectChanged -= RefreshLabel;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.newSceneCreated -= OnNewSceneCreated;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Disabled from ExitingEditMode until EnteredEditMode: no switching while in (or entering) Play Mode.
            SetEnabled(state == PlayModeStateChange.EnteredEditMode);
            RefreshLabel();
        }

        private void OnActiveSceneChanged(Scene previous, Scene current) => RefreshLabel();
        private void OnSceneOpened(Scene scene, OpenSceneMode mode) => RefreshLabel();
        private void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode) => RefreshLabel();
        private void OnSceneSaved(Scene scene) => RefreshLabel();

        private void RefreshLabel()
        {
            // Always show the *active* scene (an additively opened scene must not replace the label).
            var scene = SceneManager.GetActiveScene();
            text = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
        }

        private void ShowMenu()
        {
            var showAll = EditorSceneSwitcher.ShowAllScenes;
            var paths = EditorSceneSwitcher.GetScenePaths();
            var labels = BuildLabels(paths, showAll);
            var activePath = SceneManager.GetActiveScene().path;

            var menu = new GenericMenu();

            if (paths.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent(showAll
                    ? "No scenes in Assets"
                    : "No enabled scenes in Build Settings"));
            }

            for (var i = 0; i < paths.Count; i++)
            {
                var scenePath = paths[i];
                // Compare paths, not names: two scenes can share a name in different folders.
                menu.AddItem(new GUIContent(labels[i]), scenePath == activePath,
                    () => EditorSceneSwitcher.OpenScene(scenePath));
            }

            menu.DropDown(worldBound);
        }

        /// <summary>
        /// "All scenes": the folder structure becomes submenus (GenericMenu splits on '/').
        /// Build Settings: plain names, with the folder appended when two scenes share a name.
        /// </summary>
        private static List<string> BuildLabels(List<string> paths, bool useFolders)
        {
            var labels = new List<string>(paths.Count);

            if (useFolders)
            {
                const string assetsPrefix = "Assets/";
                foreach (var path in paths)
                {
                    var label = path.StartsWith(assetsPrefix, StringComparison.Ordinal)
                        ? path.Substring(assetsPrefix.Length)
                        : path;
                    labels.Add(Path.ChangeExtension(label, null));
                }

                return labels;
            }

            var nameCounts = new Dictionary<string, int>();
            foreach (var path in paths)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                nameCounts[name] = nameCounts.TryGetValue(name, out var count) ? count + 1 : 1;
            }

            foreach (var path in paths)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (nameCounts[name] > 1)
                {
                    // U+2215 (division slash) looks like '/' but doesn't create a submenu.
                    var folder = (Path.GetDirectoryName(path) ?? string.Empty).Replace('\\', '/').Replace('/', '∕');
                    labels.Add($"{name}  ({folder})");
                }
                else
                {
                    labels.Add(name);
                }
            }

            return labels;
        }
    }

    [EditorToolbarElement(ID, typeof(SceneView))]
    public class AutoEnterPlayModeToggle : EditorToolbarToggle
    {
        public const string ID = SceneSwitcherToolbarOverlay.OverlayID + "/auto-enter-playmode-toggle";

        private const string Tooltip = "Auto enter Play Mode after switching scene.";

        public AutoEnterPlayModeToggle()
        {
            var content = EditorGUIUtility.TrTextContentWithIcon(string.Empty, Tooltip, "d_preAudioAutoPlayOff");
            tooltip = content.tooltip;
            icon = content.image as Texture2D;

            SetValueWithoutNotify(EditorSceneSwitcher.AutoEnterPlaymode);
            this.RegisterValueChangedCallback(evt => EditorSceneSwitcher.AutoEnterPlaymode = evt.newValue);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EditorSceneSwitcher.SettingsChanged += Sync;
                Sync();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => EditorSceneSwitcher.SettingsChanged -= Sync);
        }

        private void Sync() => SetValueWithoutNotify(EditorSceneSwitcher.AutoEnterPlaymode);
    }

    [EditorToolbarElement(ID, typeof(SceneView))]
    public class ShowAllScenesToggle : EditorToolbarToggle
    {
        public const string ID = SceneSwitcherToolbarOverlay.OverlayID + "/show-all-scenes-toggle";

        private const string Tooltip =
            "Show all scenes. Click to toggle visibility of scenes not added to Build Settings.";

        public ShowAllScenesToggle()
        {
            tooltip = Tooltip;
            Sync();

            this.RegisterValueChangedCallback(evt => EditorSceneSwitcher.ShowAllScenes = evt.newValue);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EditorSceneSwitcher.SettingsChanged += Sync;
                Sync();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => EditorSceneSwitcher.SettingsChanged -= Sync);
        }

        private void Sync()
        {
            var showAll = EditorSceneSwitcher.ShowAllScenes;
            SetValueWithoutNotify(showAll);

            var content = EditorGUIUtility.TrTextContentWithIcon(string.Empty, Tooltip,
                showAll ? "scenevis_visible_hover" : "scenevis_hidden_hover");
            icon = content.image as Texture2D;
        }
    }
}
#endif
