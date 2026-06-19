using UnityEngine;
using UnityEditor;
using System.IO;


namespace BuildReportTool.Window.Screen
{
    public class Overview : BaseScreen
    {
        private Vector2 _scrollPos = Vector2.zero;

        private bool _showTopUsed;
        private bool _showTopUnused;

        public override string Name => Labels.OVERVIEW_CATEGORY_LABEL;

        public override void RefreshData(BuildInfo buildReport)
        {
        }

        public override void DrawGUI(Rect position, BuildInfo buildReportToDisplay)
        {
            GUILayout.Space(2); // top padding for scrollbar

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10); // extra left padding


            GUILayout.BeginVertical();

            GUILayout.Space(10); // top padding


            // report title
            GUILayout.Label(buildReportToDisplay.SuitableTitle, Settings.MAIN_TITLE_STYLE_NAME);


            GUILayout.Space(10);


            // two-column layout
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.MaxWidth(350));
            GUILayout.Label(Labels.TIME_OF_BUILD_LABEL, Settings.INFO_TITLE_STYLE_NAME);
            GUILayout.Label(buildReportToDisplay.GetTimeReadable(), Settings.INFO_SUBTITLE_STYLE_NAME);

            GUILayout.Label("Report generation took:", Settings.INFO_TITLE_STYLE_NAME);
            GUILayout.Label(buildReportToDisplay.ReportGenerationTime.ToString(), Settings.INFO_SUBTITLE_STYLE_NAME);

            if (!string.IsNullOrEmpty(buildReportToDisplay.TotalBuildSize) && !string.IsNullOrEmpty(buildReportToDisplay.BuildFilePath))
            {
                GUILayout.BeginVertical();
                GUILayout.Label(Labels.BUILD_TOTAL_SIZE_LABEL, Settings.INFO_TITLE_STYLE_NAME);

                GUILayout.Label(Util.GetBuildSizePathDescription(buildReportToDisplay), Settings.TINY_HELP_STYLE_NAME);

                GUILayout.Label(buildReportToDisplay.TotalBuildSize, Settings.BIG_NUMBER_STYLE_NAME);
                GUILayout.EndVertical();
            }

            GUILayout.Space(20);

            string emphasisColor = "black";
            if (EditorGUIUtility.isProSkin)
            {
                emphasisColor = "white";
            }

            GUILayout.Label("<color=" + emphasisColor + "><size=20><b>" + buildReportToDisplay.BuildSizes[1].Name + "</b></size></color> are the largest,\ntaking up <color=" + emphasisColor + "><size=20><b>" + buildReportToDisplay.BuildSizes[1].Percentage + "%</b></size></color> of the build" + (!buildReportToDisplay.HasStreamingAssets ? "\n<size=12>(not counting streaming assets)</size>" : ""), Settings.INFO_TEXT_STYLE_NAME);
            GUILayout.Space(20);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.MaxWidth(350));
            GUILayout.Label("Made for:", Settings.INFO_TITLE_STYLE_NAME);
            GUILayout.Label(buildReportToDisplay.BuildType, Settings.INFO_SUBTITLE_STYLE_NAME);

            GUILayout.Label("Built in:", Settings.INFO_TITLE_STYLE_NAME);
            GUILayout.Label(buildReportToDisplay.UnityVersionDisplayed, Settings.INFO_SUBTITLE_STYLE_NAME);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();

            int numberOfTopUsed = buildReportToDisplay.HasUsedAssets ? buildReportToDisplay.UsedAssets.NumberOfTopLargest : 0;
            int numberOfTopUnused = buildReportToDisplay.HasUnusedAssets ? buildReportToDisplay.UnusedAssets.NumberOfTopLargest : 0;
            if (Event.current.type == EventType.Layout)
            {
                _showTopUsed = numberOfTopUsed > 0 && buildReportToDisplay.UsedAssets.TopLargest != null;
                _showTopUnused = numberOfTopUnused > 0 && buildReportToDisplay.UnusedAssets.TopLargest != null;
            }

            GUILayout.BeginVertical();
            if (_showTopUsed)
            {
                GUILayout.Label(string.Format("Top {0} largest in build:", numberOfTopUsed), Settings.INFO_TITLE_STYLE_NAME);

                if (!BuildReportTool.Options.AutoResortAssetsWhenUnityEditorRegainsFocus && GUILayout.Button("Refresh", GUILayout.Height(20), GUILayout.MaxWidth(520)))
                {
                    buildReportToDisplay.RecategorizeUsedAssets();
                    buildReportToDisplay.FlagOkToRefresh();
                }

                DrawAssetList(buildReportToDisplay.UsedAssets, true);
            }
            GUILayout.EndVertical();

            GUILayout.Space(50);

            GUILayout.BeginVertical();
            if (_showTopUnused)
            {
                GUILayout.Label(string.Format("Top {0} largest not in build:", numberOfTopUnused), Settings.INFO_TITLE_STYLE_NAME);

                if (!BuildReportTool.Options.AutoResortAssetsWhenUnityEditorRegainsFocus && GUILayout.Button("Refresh", GUILayout.Height(20), GUILayout.MaxWidth(520)))
                {
                    buildReportToDisplay.RecategorizeUnusedAssets();
                    buildReportToDisplay.FlagOkToRefresh();
                }

                DrawAssetList(buildReportToDisplay.UnusedAssets, false);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();


            GUILayout.Space(20);


            GUILayout.EndVertical();

            GUILayout.Space(20); // extra right padding
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }

        private void DrawAssetList(BuildReportTool.AssetList assetList, bool usedAssets)
        {
            if (assetList == null || assetList.TopLargest == null)
            {
                //Debug.LogError("no top ten largest");
                return;
            }

            SizePart[] assetsToShow = assetList.TopLargest;

            if (assetsToShow == null)
            {
                //Debug.LogError("no top ten largest");
                return;
            }

            bool useAlt = true;

            GUILayout.BeginHorizontal();

            // 1st column: name
            GUILayout.BeginVertical();
            for (int n = 0; n < assetsToShow.Length; ++n)
            {
                SizePart b = assetsToShow[n];

                string styleToUse = useAlt ? Settings.LIST_NORMAL_ALT_STYLE_NAME : Settings.LIST_NORMAL_STYLE_NAME;
                string iconStyleToUse = useAlt ? Settings.LIST_ICON_ALT_STYLE_NAME : Settings.LIST_ICON_STYLE_NAME;

                string prettyName = " " + (n + 1) + ". " + Path.GetFileName(b.Name);
                Texture icon = AssetDatabase.GetCachedIcon(b.Name);

                GUILayout.BeginHorizontal();
                if (icon == null)
                {
                    //GUILayout.Space(22);
                    GUILayout.Label(string.Empty, iconStyleToUse, GUILayout.Width(28), GUILayout.Height(30));
                }
                else
                {
                    GUILayout.Button(icon, iconStyleToUse, GUILayout.Width(28), GUILayout.Height(30));
                }
                if (GUILayout.Button(prettyName, styleToUse, GUILayout.MinWidth(100), GUILayout.MaxWidth(400), GUILayout.Height(30)))
                {
                    Utility.PingAssetInProject(b.Name);
                }
                GUILayout.EndHorizontal();

                useAlt = !useAlt;
            }
            GUILayout.EndVertical();

            // 2nd column: size

            bool useRawSize = usedAssets && !BuildReportTool.Options.ShowImportedSizeForUsedAssets || !usedAssets;

            useAlt = true;
            GUILayout.BeginVertical();
            for (int n = 0; n < assetsToShow.Length; ++n)
            {
                SizePart b = assetsToShow[n];

                string styleToUse = useAlt ? Settings.LIST_NORMAL_ALT_STYLE_NAME : Settings.LIST_NORMAL_STYLE_NAME;

                GUILayout.Label(useRawSize ? b.RawSize : b.ImportedSize, styleToUse, GUILayout.MaxWidth(100), GUILayout.Height(30));

                useAlt = !useAlt;
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}