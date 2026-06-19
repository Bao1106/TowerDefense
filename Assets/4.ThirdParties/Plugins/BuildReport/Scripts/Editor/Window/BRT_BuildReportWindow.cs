//#define BRT_SHOW_MINOR_WARNINGS

#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using System.Threading;
using BuildReportTool;
using BuildReportTool.Window;

public class BRT_BuildReportWindow : EditorWindow
{
    private void OnDisable()
    {
        ForceStopFileLoadThread();
        IsOpen = false;
    }

    private void OnFocus()
    {
        if (Options.AutoResortAssetsWhenUnityEditorRegainsFocus)
        {
            _usedAssetsScreen.RefreshData(_buildInfo);
            _unusedAssetsScreen.RefreshData(_buildInfo);

            // check if configured file filters changed and only then do we need to recategorize

            if (Options.ShouldUseConfiguredFileFilters())
            {
                RecategorizeDisplayedBuildInfo();
            }
        }
    }

    private void OnEnable()
    {
        //Debug.Log("BuildReportWindow.OnEnable() " + System.DateTime.Now);


        IsOpen = true;

        InitGUISkin();


        if (Util.BuildInfoHasContents(_buildInfo))
        {
            //Debug.Log("recompiled " + _buildInfo.SavedPath);
            if (!string.IsNullOrEmpty(_buildInfo.SavedPath))
            {
                BuildInfo loadedBuild = Util.OpenSerializedBuildInfo(_buildInfo.SavedPath);
                if (Util.BuildInfoHasContents(loadedBuild))
                {
                    _buildInfo = loadedBuild;
                }
            }
            else
            {
                if (_buildInfo.HasUsedAssets)
                {
                    _buildInfo.UsedAssets.AssignPerCategoryList(ReportGenerator.SegregateAssetSizesPerCategory(_buildInfo.UsedAssets.All, _buildInfo.FileFilters));
                }
                if (_buildInfo.HasUnusedAssets)
                {
                    _buildInfo.UnusedAssets.AssignPerCategoryList(ReportGenerator.SegregateAssetSizesPerCategory(_buildInfo.UnusedAssets.All, _buildInfo.FileFilters));
                }
            }
        }

        // lol wtf have I done
        _usedAssetsScreen.SetListToDisplay(BuildReportTool.Window.Screen.AssetList.ListToDisplay.UsedAssets);
        _unusedAssetsScreen.SetListToDisplay(BuildReportTool.Window.Screen.AssetList.ListToDisplay.UnusedAssets);

        _overviewScreen.RefreshData(_buildInfo);
        _buildSettingsScreen.RefreshData(_buildInfo);
        _sizeStatsScreen.RefreshData(_buildInfo);
        _usedAssetsScreen.RefreshData(_buildInfo);
        _unusedAssetsScreen.RefreshData(_buildInfo);

        _optionsScreen.RefreshData(_buildInfo);
        _helpScreen.RefreshData(_buildInfo);
    }

    private double _lastTime;

    private void Update()
    {
        double deltaTime = EditorApplication.timeSinceStartup - _lastTime;
        _lastTime = EditorApplication.timeSinceStartup;

        _usedAssetsScreen.Update(EditorApplication.timeSinceStartup, deltaTime, _buildInfo);
        _unusedAssetsScreen.Update(EditorApplication.timeSinceStartup, deltaTime, _buildInfo);

        if (_buildInfo != null && ReportGenerator.IsFinishedGettingValuesFromThread)
        {
            OnFinishGeneratingBuildReport();
        }

        if (Util.ShouldGetBuildReportNow && !ReportGenerator.IsGettingValuesFromThread && !EditorApplication.isCompiling)
        {
            //Debug.Log("BuildReportWindow getting build info right after the build... " + System.DateTime.Now);
            Refresh();
            GoToOverviewScreen();
        }

        if (_finishedOpeningFromThread)
        {
            OnFinishOpeningBuildReportFile();
        }

        if (_buildInfo != null)
        {
            if (_buildInfo.RequestedToRefresh)
            {
                Repaint();
                _buildInfo.FlagFinishedRefreshing();
            }
        }
    }

    // ==========================================================================================
    // sub-screens

    private readonly BuildReportTool.Window.Screen.Overview _overviewScreen = new BuildReportTool.Window.Screen.Overview();

    private readonly BuildReportTool.Window.Screen.BuildSettings _buildSettingsScreen = new BuildReportTool.Window.Screen.BuildSettings();

    private readonly BuildReportTool.Window.Screen.SizeStats _sizeStatsScreen = new BuildReportTool.Window.Screen.SizeStats();

    private readonly BuildReportTool.Window.Screen.AssetList _usedAssetsScreen = new BuildReportTool.Window.Screen.AssetList();

    private readonly BuildReportTool.Window.Screen.AssetList _unusedAssetsScreen = new BuildReportTool.Window.Screen.AssetList();

    private readonly BuildReportTool.Window.Screen.Options _optionsScreen = new BuildReportTool.Window.Screen.Options();
    private readonly BuildReportTool.Window.Screen.Help _helpScreen = new BuildReportTool.Window.Screen.Help();


    // ==========================================================================================


    public static string GetValueMessage { set; get; }

    private static bool _loadingValuesFromThread;
    public static bool LoadingValuesFromThread => _loadingValuesFromThread;

    private static bool _noGuiSkinFound;

    [SerializeField]
    private static BuildInfo _buildInfo;


    private GUISkin _usedSkin = null;


    public static bool IsOpen { get; set; }


    private Texture2D _toolbarIconLog;
    private Texture2D _toolbarIconOpen;
    private Texture2D _toolbarIconSave;
    private Texture2D _toolbarIconOptions;
    private Texture2D _toolbarIconHelp;


    private GUIContent _toolbarLabelLog;
    private GUIContent _toolbarLabelOpen;
    private GUIContent _toolbarLabelSave;
    private GUIContent _toolbarLabelOptions;
    private GUIContent _toolbarLabelHelp;


    private void RecategorizeDisplayedBuildInfo()
    {
        if (Util.BuildInfoHasContents(_buildInfo))
        {
            ReportGenerator.RecategorizeAssetList(_buildInfo);
        }
    }


    private void InitGUISkin()
    {
        string guiSkinToUse = BuildReportTool.Window.Settings.DEFAULT_GUI_SKIN_FILENAME;
        if (EditorGUIUtility.isProSkin)
        {
            guiSkinToUse = BuildReportTool.Window.Settings.DARK_GUI_SKIN_FILENAME;
        }

        // try default path
        _usedSkin = AssetDatabase.LoadAssetAtPath(Options.BUILD_REPORT_TOOL_DEFAULT_PATH + "/GUI/" + guiSkinToUse, typeof(GUISkin)) as GUISkin;

        if (_usedSkin == null)
        {
#if BRT_SHOW_MINOR_WARNINGS
			Debug.LogWarning(BuildReportTool.Options.BUILD_REPORT_PACKAGE_MOVED_MSG);
#endif

            string folderPath = Util.FindAssetFolder(Application.dataPath, Options.BUILD_REPORT_TOOL_DEFAULT_FOLDER_NAME);
            if (!string.IsNullOrEmpty(folderPath))
            {
                folderPath = folderPath.Replace('\\', '/');
                int assetsIdx = folderPath.IndexOf("/Assets/");
                if (assetsIdx != -1)
                {
                    folderPath = folderPath.Substring(assetsIdx + 8, folderPath.Length - assetsIdx - 8);
                }
                //Debug.Log(folderPath);

                _usedSkin = AssetDatabase.LoadAssetAtPath("Assets/" + folderPath + "/GUI/" + guiSkinToUse, typeof(GUISkin)) as GUISkin;
            }
            else
            {
                Debug.LogError(Options.BUILD_REPORT_PACKAGE_MISSING_MSG);
            }
            //Debug.Log("_usedSkin " + (_usedSkin != null));
        }

        if (_usedSkin != null)
        {
            if (!EditorGUIUtility.isProSkin)
            {
                GUISkin nativeSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);

                _usedSkin.verticalScrollbar = nativeSkin.verticalScrollbar;
                _usedSkin.verticalScrollbarThumb = nativeSkin.verticalScrollbarThumb;
                _usedSkin.verticalScrollbarUpButton = nativeSkin.verticalScrollbarUpButton;
                _usedSkin.verticalScrollbarDownButton = nativeSkin.verticalScrollbarDownButton;

                _usedSkin.horizontalScrollbar = nativeSkin.horizontalScrollbar;
                _usedSkin.horizontalScrollbarThumb = nativeSkin.horizontalScrollbarThumb;
                _usedSkin.horizontalScrollbarLeftButton = nativeSkin.horizontalScrollbarLeftButton;
                _usedSkin.horizontalScrollbarRightButton = nativeSkin.horizontalScrollbarRightButton;

                // change the toggle skin to use the Unity builtin look, but keep our settings
                GUIStyle toggleSaved = new GUIStyle(_usedSkin.toggle);

                // make our own copy of the native skin toggle so that editing it won't affect the rest of the editor GUI
                GUIStyle nativeToggleCopy = new GUIStyle(nativeSkin.toggle);

                _usedSkin.toggle = nativeToggleCopy;
                _usedSkin.toggle.border = toggleSaved.border;
                _usedSkin.toggle.margin = toggleSaved.margin;
                _usedSkin.toggle.padding = toggleSaved.padding;
                _usedSkin.toggle.overflow = toggleSaved.overflow;
                _usedSkin.toggle.contentOffset = toggleSaved.contentOffset;

                _usedSkin.box = nativeSkin.box;
                _usedSkin.label = nativeSkin.label;
                _usedSkin.textField = nativeSkin.textField;
                _usedSkin.button = nativeSkin.button;


                _usedSkin.label.wordWrap = true;
            }

            // ----------------------------------------------------

            _toolbarIconLog = _usedSkin.GetStyle("Icon-Toolbar-Log").normal.background;
            _toolbarIconOpen = _usedSkin.GetStyle("Icon-Toolbar-Open").normal.background;
            _toolbarIconSave = _usedSkin.GetStyle("Icon-Toolbar-Save").normal.background;
            _toolbarIconOptions = _usedSkin.GetStyle("Icon-Toolbar-Options").normal.background;
            _toolbarIconHelp = _usedSkin.GetStyle("Icon-Toolbar-Help").normal.background;

            _toolbarLabelLog = new GUIContent(Labels.REFRESH_LABEL, _toolbarIconLog);
            _toolbarLabelOpen = new GUIContent(Labels.OPEN_LABEL, _toolbarIconOpen);
            _toolbarLabelSave = new GUIContent(Labels.SAVE_LABEL, _toolbarIconSave);
            _toolbarLabelOptions = new GUIContent(Labels.OPTIONS_CATEGORY_LABEL, _toolbarIconOptions);
            _toolbarLabelHelp = new GUIContent(Labels.HELP_CATEGORY_LABEL, _toolbarIconHelp);
        }
    }


    public void Init(BuildInfo buildInfo)
    {
        _buildInfo = buildInfo;

        minSize = new Vector2(903, 378);
    }

    private void Refresh()
    {
        GoToOverviewScreen();
        ReportGenerator.RefreshData(ref _buildInfo);
    }

    private bool IsWaitingForBuildCompletionToGenerateBuildReport => Util.ShouldGetBuildReportNow && EditorApplication.isCompiling;

    private void OnFinishOpeningBuildReportFile()
    {
        _finishedOpeningFromThread = false;

        if (Util.BuildInfoHasContents(_buildInfo))
        {
            _buildSettingsScreen.RefreshData(_buildInfo);
            _usedAssetsScreen.RefreshData(_buildInfo);
            _unusedAssetsScreen.RefreshData(_buildInfo);
            _sizeStatsScreen.RefreshData(_buildInfo);


            _buildInfo.OnDeserialize();
            _buildInfo.SetSavedPath(_lastOpenedBuildInfoFilePath);
        }
        Repaint();
        GoToOverviewScreen();
    }

    private void OnFinishGeneratingBuildReport()
    {
        ReportGenerator.OnFinishedGetValues(_buildInfo);
        _buildInfo.UnescapeAssetNames();
        GoToOverviewScreen();

        _buildSettingsScreen.RefreshData(_buildInfo);
    }


    private void GoToOverviewScreen()
    {
        _selectedCategoryIdx = OVERVIEW_IDX;
    }


    // ==========================================================================

    private void DrawOverviewScreen()
    {
        _overviewScreen.DrawGUI(position, _buildInfo);
    }

    private void DrawBuildSettingsScreen()
    {
        _buildSettingsScreen.DrawGUI(position, _buildInfo);
    }

    private void DrawSizeStatsScreen()
    {
        _sizeStatsScreen.DrawGUI(position, _buildInfo);
    }

    private void DrawOptionsScreen()
    {
        _optionsScreen.DrawGUI(position, _buildInfo);
    }

    private void DrawHelpScreen()
    {
        _helpScreen.DrawGUI(position, _buildInfo);
    }

    // ==========================================================================


    private int _fileFilterGroupToUseOnOpeningOptionsWindow = 0;
    private int _fileFilterGroupToUseOnClosingOptionsWindow = 0;


    private int _selectedCategoryIdx = 0;

    private bool IsInOverviewCategory => _selectedCategoryIdx == OVERVIEW_IDX;

    private bool IsInBuildSettingsCategory => _selectedCategoryIdx == BUILD_SETTINGS_IDX;

    private bool IsInSizeStatsCategory => _selectedCategoryIdx == SIZE_STATS_IDX;

    private bool IsInUsedAssetsCategory => _selectedCategoryIdx == USED_ASSETS_IDX;

    private bool IsInUnusedAssetsCategory => _selectedCategoryIdx == UNUSED_ASSETS_IDX;

    private bool IsInOptionsCategory => _selectedCategoryIdx == OPTIONS_IDX;

    private bool IsInHelpCategory => _selectedCategoryIdx == HELP_IDX;


    private const int OVERVIEW_IDX = 0;
    private const int BUILD_SETTINGS_IDX = 1;
    private const int SIZE_STATS_IDX = 2;
    private const int USED_ASSETS_IDX = 3;
    private const int UNUSED_ASSETS_IDX = 4;

    private const int OPTIONS_IDX = 5;
    private const int HELP_IDX = 6;


    private bool _finishedOpeningFromThread = false;
    private string _lastOpenedBuildInfoFilePath = "";

    private void _OpenBuildInfo(string filepath)
    {
        if (string.IsNullOrEmpty(filepath))
        {
            return;
        }

        _finishedOpeningFromThread = false;
        GetValueMessage = "Opening...";
        BuildInfo loadedBuild = Util.OpenSerializedBuildInfo(filepath, false);


        if (Util.BuildInfoHasContents(loadedBuild))
        {
            _buildInfo = loadedBuild;
            _lastOpenedBuildInfoFilePath = filepath;
        }
        else
        {
            Debug.LogError("Build Report Tool: Invalid data in build info file: " + filepath);
        }

        _finishedOpeningFromThread = true;

        GetValueMessage = "";
    }


    private Thread _currentBuildReportFileLoadThread = null;

    private bool IsCurrentlyOpeningAFile => _currentBuildReportFileLoadThread != null && _currentBuildReportFileLoadThread.ThreadState == ThreadState.Running;

    private void ForceStopFileLoadThread()
    {
        if (IsCurrentlyOpeningAFile)
        {
            try
            {
                //Debug.LogFormat(this, "Build Report Tool: Stopping file load background thread...");
                _currentBuildReportFileLoadThread.Abort();
                Debug.LogFormat(this, "Build Report Tool: File load background thread stopped.");
            }
            catch (ThreadStateException)
            {
            }
        }
    }

    private void OpenBuildInfoAsync(string filepath)
    {
        if (string.IsNullOrEmpty(filepath))
        {
            return;
        }

        if (!Options.UseThreadedFileLoading)
        {
            _OpenBuildInfo(filepath);
        }
        else
        {
            if (_currentBuildReportFileLoadThread != null && _currentBuildReportFileLoadThread.ThreadState == ThreadState.Running)
            {
                ForceStopFileLoadThread();
            }
            _currentBuildReportFileLoadThread = new Thread(() => LoadThread(filepath));
            _currentBuildReportFileLoadThread.Start();
            Debug.LogFormat(this, "Build Report Tool: Started new load background thread...");
        }
    }

    private void LoadThread(string filepath)
    {
        _OpenBuildInfo(filepath);
        Debug.LogFormat(this, "Build Report Tool: Load background thread finished.");
    }


    private void DrawCentralMessage(string msg)
    {
        float w = 300;
        float h = 100;
        float x = (position.width - w) * 0.5f;
        float y = (position.height - h) * 0.25f;

        GUI.Label(new Rect(x, y, w, h), msg);
    }


    private void DrawWarningMessage(string msg)
    {
        float w = 400;
        float h = 100;
        float x = (position.width - w) * 0.5f;
        float y = (position.height - h) * 0.25f + 100 + 40;

        Rect msgRect = new Rect(x, y, w, h);
        GUI.Label(msgRect, msg);

        GUIStyle warning = GUI.skin.GetStyle("Icon-Warning");
        if (warning != null)
        {
            Texture2D warningIcon = warning.normal.background;

            float iconWidth = warning.fixedWidth;
            float iconHeight = warning.fixedHeight;

            GUI.DrawTexture(new Rect(msgRect.x - iconWidth, msgRect.y, iconWidth, iconHeight), warningIcon);
        }
    }


    private void DrawTopRowButtons()
    {
        int toolbarX = 10;

        if (GUI.Button(new Rect(toolbarX, 5, 50, 40), _toolbarLabelLog, BuildReportTool.Window.Settings.TOOLBAR_LEFT_STYLE_NAME) && !LoadingValuesFromThread)
        {
            Refresh();
        }
        toolbarX += 50;
        if (GUI.Button(new Rect(toolbarX, 5, 40, 40), _toolbarLabelOpen, BuildReportTool.Window.Settings.TOOLBAR_MIDDLE_STYLE_NAME) && !LoadingValuesFromThread)
        {
            string filepath = EditorUtility.OpenFilePanel(Labels.OPEN_SERIALIZED_BUILD_INFO_TITLE, Options.BuildReportSavePath, "xml");

            OpenBuildInfoAsync(filepath);
        }
        toolbarX += 40;

        if (GUI.Button(new Rect(toolbarX, 5, 40, 40), _toolbarLabelSave, BuildReportTool.Window.Settings.TOOLBAR_RIGHT_STYLE_NAME) && Util.BuildInfoHasContents(_buildInfo))
        {
            string filepath = EditorUtility.SaveFilePanel(Labels.SAVE_MSG, Options.BuildReportSavePath, _buildInfo.GetDefaultFilename(), "xml");

            if (!string.IsNullOrEmpty(filepath))
            {
                Util.SerializeBuildInfo(_buildInfo, filepath);
            }
        }
        toolbarX += 40;


        toolbarX += 20;

        //if (!BuildReportTool.Util.BuildInfoHasContents(_buildInfo))
        {
            if (GUI.Button(new Rect(toolbarX, 5, 55, 40), _toolbarLabelOptions, BuildReportTool.Window.Settings.TOOLBAR_LEFT_STYLE_NAME))
            {
                _selectedCategoryIdx = OPTIONS_IDX;
            }
            toolbarX += 55;
            if (GUI.Button(new Rect(toolbarX, 5, 70, 40), _toolbarLabelHelp, BuildReportTool.Window.Settings.TOOLBAR_RIGHT_STYLE_NAME))
            {
                _selectedCategoryIdx = HELP_IDX;
            }
        }
    }

    private bool _buildInfoHasNoContentsToDisplay = false;

    private void OnGUI()
    {
        if (Event.current.type == EventType.Layout)
        {
            _noGuiSkinFound = _usedSkin == null;
            _loadingValuesFromThread = !string.IsNullOrEmpty(GetValueMessage);
            _buildInfoHasNoContentsToDisplay = !Util.BuildInfoHasContents(_buildInfo);
        }

        //GUI.Label(new Rect(5, 100, 800, 20), "BuildReportTool.Util.ShouldReload: " + BuildReportTool.Util.ShouldReload + " EditorApplication.isCompiling: " + EditorApplication.isCompiling);
        if (_noGuiSkinFound)
        {
            GUI.Label(new Rect(20, 20, 500, 100), Options.BUILD_REPORT_PACKAGE_MISSING_MSG);
            return;
        }

        GUI.skin = _usedSkin;

        DrawTopRowButtons();


        GUI.Label(new Rect(0, 0, position.width, 20), Info.ReadableVersion, BuildReportTool.Window.Settings.VERSION_STYLE_NAME);


        // loading message
        if (LoadingValuesFromThread)
        {
            DrawCentralMessage(GetValueMessage);
            return;
        }

        // content to show when there is no build report on display
        if (_buildInfoHasNoContentsToDisplay)
        {
            if (IsInOptionsCategory)
            {
                GUILayout.Space(40);
                DrawOptionsScreen();
            }
            else if (IsInHelpCategory)
            {
                GUILayout.Space(40);
                DrawHelpScreen();
            }
            else if (IsWaitingForBuildCompletionToGenerateBuildReport)
            {
                DrawCentralMessage(Labels.WAITING_FOR_BUILD_TO_COMPLETE_MSG);
            }
            else
            {
                DrawCentralMessage(Labels.NO_BUILD_INFO_FOUND_MSG);

                if (ReportGenerator.CheckIfUnityHasNoLogArgument())
                {
                    DrawWarningMessage(Labels.FOUND_NO_LOG_ARGUMENT_MSG);
                }
            }

            return;
        }


        GUILayout.Space(50); // top padding (top row buttons are 40 pixels)


        // category buttons

        int oldSelectedCategoryIdx = _selectedCategoryIdx;

        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(IsInOverviewCategory, "Overview", BuildReportTool.Window.Settings.TAB_LEFT_STYLE_NAME, GUILayout.ExpandWidth(true)))
        {
            _selectedCategoryIdx = OVERVIEW_IDX;
        }
        if (GUILayout.Toggle(IsInBuildSettingsCategory, "Project Settings", BuildReportTool.Window.Settings.TAB_MIDDLE_STYLE_NAME, GUILayout.ExpandWidth(true)))
        {
            _selectedCategoryIdx = BUILD_SETTINGS_IDX;
        }
        if (GUILayout.Toggle(IsInSizeStatsCategory, "Size Stats", BuildReportTool.Window.Settings.TAB_MIDDLE_STYLE_NAME, GUILayout.ExpandWidth(true)))
        {
            _selectedCategoryIdx = SIZE_STATS_IDX;
        }
        if (GUILayout.Toggle(IsInUsedAssetsCategory, "Used Assets", BuildReportTool.Window.Settings.TAB_MIDDLE_STYLE_NAME, GUILayout.ExpandWidth(true)))
        {
            _selectedCategoryIdx = USED_ASSETS_IDX;
        }
        if (GUILayout.Toggle(IsInUnusedAssetsCategory, "Unused Assets", BuildReportTool.Window.Settings.TAB_RIGHT_STYLE_NAME, GUILayout.ExpandWidth(true)))
        {
            _selectedCategoryIdx = UNUSED_ASSETS_IDX;
        }

        /*GUILayout.Space(20);

		if (GUILayout.Toggle(IsInOptionsCategory, _toolbarLabelOptions, BuildReportTool.Window.Settings.TAB_LEFT_STYLE_NAME, GUILayout.ExpandWidth(true)))
		{
			_selectedCategoryIdx = OPTIONS_IDX;
		}
		if (GUILayout.Toggle(IsInHelpCategory, _toolbarLabelHelp, BuildReportTool.Window.Settings.TAB_RIGHT_STYLE_NAME, GUILayout.ExpandWidth(true)))
		{
			_selectedCategoryIdx = HELP_IDX;
		}*/
        GUILayout.EndHorizontal();


        if (oldSelectedCategoryIdx != OPTIONS_IDX && _selectedCategoryIdx == OPTIONS_IDX)
        {
            // moving into the options screen
            _fileFilterGroupToUseOnOpeningOptionsWindow = Options.FilterToUseInt;
        }
        else if (oldSelectedCategoryIdx == OPTIONS_IDX && _selectedCategoryIdx != OPTIONS_IDX)
        {
            // moving away from the options screen
            _fileFilterGroupToUseOnClosingOptionsWindow = Options.FilterToUseInt;

            if (_fileFilterGroupToUseOnOpeningOptionsWindow != _fileFilterGroupToUseOnClosingOptionsWindow)
            {
                RecategorizeDisplayedBuildInfo();
            }
        }


        // main content
        GUILayout.BeginHorizontal();
        //GUILayout.Space(3); // left padding
        GUILayout.BeginVertical();

        if (IsInOverviewCategory)
        {
            DrawOverviewScreen();
        }
        else if (IsInBuildSettingsCategory)
        {
            DrawBuildSettingsScreen();
        }
        else if (IsInSizeStatsCategory)
        {
            DrawSizeStatsScreen();
        }
        else if (IsInUsedAssetsCategory)
        {
            _usedAssetsScreen.DrawGUI(position, _buildInfo);
        }
        else if (IsInUnusedAssetsCategory)
        {
            _unusedAssetsScreen.DrawGUI(position, _buildInfo);
        }
        else if (IsInOptionsCategory)
        {
            DrawOptionsScreen();
        }
        else if (IsInHelpCategory)
        {
            DrawHelpScreen();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        //GUILayout.Space(5); // right padding
        GUILayout.EndHorizontal();


        //GUILayout.Space(10); // bottom padding
    }
}

#endif