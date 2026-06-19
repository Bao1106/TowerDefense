using UnityEngine;


namespace BuildReportTool.Window.Screen
{
    public class SizeStats : BaseScreen
    {
        public override string Name => Labels.SIZE_STATS_CATEGORY_LABEL;

        public override void RefreshData(BuildInfo buildReport)
        {
        }

        private Vector2 _assetListScrollPos;


        private bool _hasTotalBuildSize;
        private bool _hasUsedAssetsTotalSize;
        private bool _hasBuildSizes;
        private bool _hasCompressedBuildSize;
        private bool _hasMonoDLLsToDisplay;
        private bool _hasUnityEngineDLLsToDisplay;
        private bool _hasScriptDLLsToDisplay;


        public override void DrawGUI(Rect position, BuildInfo buildReportToDisplay)
        {
            if (Event.current.type == EventType.Layout)
            {
                _hasTotalBuildSize = !string.IsNullOrEmpty(buildReportToDisplay.TotalBuildSize) && !string.IsNullOrEmpty(buildReportToDisplay.BuildFilePath);

                _hasUsedAssetsTotalSize = !string.IsNullOrEmpty(buildReportToDisplay.UsedTotalSize);
                _hasCompressedBuildSize = !string.IsNullOrEmpty(buildReportToDisplay.CompressedBuildSize);
                _hasBuildSizes = buildReportToDisplay.BuildSizes != null;
                _hasMonoDLLsToDisplay = buildReportToDisplay.MonoDLLs != null && buildReportToDisplay.MonoDLLs.Length > 0;

                _hasUnityEngineDLLsToDisplay = buildReportToDisplay.UnityEngineDLLs != null && buildReportToDisplay.UnityEngineDLLs.Length > 0;

                _hasScriptDLLsToDisplay = buildReportToDisplay.ScriptDLLs != null && buildReportToDisplay.ScriptDLLs.Length > 0;
            }


            GUILayout.Space(2); // top padding for scrollbar

            _assetListScrollPos = GUILayout.BeginScrollView(_assetListScrollPos);

            GUILayout.Space(10); // top padding for content

            GUILayout.BeginHorizontal();
            GUILayout.Space(10); // extra left padding

            DrawTotalSize(buildReportToDisplay);

            GUILayout.Space(Settings.CATEGORY_HORIZONTAL_SPACING);
            GUILayout.BeginVertical();

            DrawBuildSizes(buildReportToDisplay);

            GUILayout.Space(Settings.CATEGORY_VERTICAL_SPACING);

            DrawDLLList(buildReportToDisplay);

            GUILayout.EndVertical();
            GUILayout.Space(20); // extra right padding
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }


        private void DrawTotalSize(BuildInfo buildReportToDisplay)
        {
            GUILayout.BeginVertical();


            if (buildReportToDisplay.HasOldSizeValues)
            {
                // in old sizes:
                // TotalBuildSize is really the used assets size
                // CompressedBuildSize if present is the total build size

                Utility.DrawLargeSizeDisplay(Labels.USED_TOTAL_SIZE_LABEL, Labels.USED_TOTAL_SIZE_DESC, buildReportToDisplay.TotalBuildSize);
                GUILayout.Space(40);
                Utility.DrawLargeSizeDisplay(Labels.BUILD_TOTAL_SIZE_LABEL, Utility.GetProperBuildSizeDesc(buildReportToDisplay), buildReportToDisplay.CompressedBuildSize);
            }
            else
            {
                // Total Build Size
                if (_hasTotalBuildSize)
                {
                    GUILayout.BeginVertical();

                    BuildPlatform buildPlatform = ReportGenerator.GetBuildPlatformFromString(buildReportToDisplay.BuildType, buildReportToDisplay.BuildTargetUsed);

                    GUILayout.Label(buildPlatform == BuildPlatform.iOS ? Labels.BUILD_XCODE_SIZE_LABEL : Labels.BUILD_TOTAL_SIZE_LABEL, Settings.INFO_TITLE_STYLE_NAME);

                    GUILayout.Label(Util.GetBuildSizePathDescription(buildReportToDisplay), Settings.TINY_HELP_STYLE_NAME);

                    GUILayout.Label(buildReportToDisplay.TotalBuildSize, Settings.BIG_NUMBER_STYLE_NAME);
                    GUILayout.EndVertical();

                    DrawAuxiliaryBuildSizes(buildReportToDisplay);
                    GUILayout.Space(40);
                }


                // Used Assets
                if (_hasUsedAssetsTotalSize)
                {
                    Utility.DrawLargeSizeDisplay(Labels.USED_TOTAL_SIZE_LABEL, Labels.USED_TOTAL_SIZE_DESC, buildReportToDisplay.UsedTotalSize);
                    GUILayout.Space(40);
                }


                // Unused Assets
                if (buildReportToDisplay.UnusedAssetsIncludedInCreation)
                {
                    Utility.DrawLargeSizeDisplay(Labels.UNUSED_TOTAL_SIZE_LABEL, Labels.UNUSED_TOTAL_SIZE_DESC, buildReportToDisplay.UnusedTotalSize);
                }
            }

            GUILayout.EndVertical();
        }


        private void DrawAuxiliaryBuildSizes(BuildInfo buildReportToDisplay)
        {
            BuildPlatform buildPlatform = ReportGenerator.GetBuildPlatformFromString(buildReportToDisplay.BuildType, buildReportToDisplay.BuildTargetUsed);

            if (buildPlatform == BuildPlatform.Web)
            {
                GUILayout.Space(20);
                GUILayout.BeginVertical();
                GUILayout.Label(Labels.WEB_UNITY3D_FILE_SIZE_LABEL, Settings.INFO_SUBTITLE_BOLD_STYLE_NAME);
                GUILayout.Label(buildReportToDisplay.WebFileBuildSize, Settings.BIG_NUMBER_STYLE_NAME);
                GUILayout.EndVertical();
            }
            else if (buildPlatform == BuildPlatform.Android)
            {
                if (!buildReportToDisplay.AndroidCreateProject && buildReportToDisplay.AndroidUseAPKExpansionFiles)
                {
                    GUILayout.Space(20);
                    GUILayout.BeginVertical();
                    GUILayout.Label(Labels.ANDROID_APK_FILE_SIZE_LABEL, Settings.INFO_SUBTITLE_BOLD_STYLE_NAME);
                    GUILayout.Label(buildReportToDisplay.AndroidApkFileBuildSize, Settings.INFO_TITLE_STYLE_NAME);
                    GUILayout.EndVertical();

                    GUILayout.Space(20);
                    GUILayout.BeginVertical();
                    GUILayout.Label(Labels.ANDROID_OBB_FILE_SIZE_LABEL, Settings.INFO_SUBTITLE_BOLD_STYLE_NAME);
                    GUILayout.Label(buildReportToDisplay.AndroidObbFileBuildSize, Settings.INFO_TITLE_STYLE_NAME);
                    GUILayout.EndVertical();
                }
                else if (buildReportToDisplay.AndroidCreateProject && buildReportToDisplay.AndroidUseAPKExpansionFiles)
                {
                    GUILayout.Space(20);
                    GUILayout.BeginVertical();
                    GUILayout.Label(Labels.ANDROID_OBB_FILE_SIZE_LABEL, Settings.INFO_SUBTITLE_BOLD_STYLE_NAME);
                    GUILayout.Label(buildReportToDisplay.AndroidObbFileBuildSize, Settings.INFO_TITLE_STYLE_NAME);
                    GUILayout.EndVertical();
                }
            }

            // Streaming Assets
            if (buildReportToDisplay.HasStreamingAssets)
            {
                GUILayout.Space(20);
                Utility.DrawLargeSizeDisplay(Labels.STREAMING_ASSETS_TOTAL_SIZE_LABEL, Labels.STREAMING_ASSETS_SIZE_DESC, buildReportToDisplay.StreamingAssetsSize);
            }
        }


        private void DrawBuildSizes(BuildInfo buildReportToDisplay)
        {
            if (_hasCompressedBuildSize)
            {
                GUILayout.BeginVertical();
            }

            GUILayout.Label(Labels.TOTAL_SIZE_BREAKDOWN_LABEL, Settings.INFO_TITLE_STYLE_NAME);

            if (_hasCompressedBuildSize)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Labels.TOTAL_SIZE_BREAKDOWN_MSG_PRE_BOLD, Settings.INFO_SUBTITLE_STYLE_NAME);
                GUILayout.Label(Labels.TOTAL_SIZE_BREAKDOWN_MSG_BOLD, Settings.INFO_SUBTITLE_BOLD_STYLE_NAME);
                GUILayout.Label(Labels.TOTAL_SIZE_BREAKDOWN_MSG_POST_BOLD, Settings.INFO_SUBTITLE_STYLE_NAME);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            if (_hasBuildSizes)
            {
                GUILayout.BeginHorizontal(GUILayout.MaxWidth(500));

                DrawNames(buildReportToDisplay.BuildSizes);
                DrawReadableSizes(buildReportToDisplay.BuildSizes);
                DrawPercentages(buildReportToDisplay.BuildSizes);

                GUILayout.EndHorizontal();
            }
        }

        private void DrawDLLList(BuildInfo buildReportToDisplay)
        {
            BuildPlatform buildPlatform = ReportGenerator.GetBuildPlatformFromString(buildReportToDisplay.BuildType, buildReportToDisplay.BuildTargetUsed);

            GUILayout.BeginHorizontal();

            // column 1
            GUILayout.BeginVertical();
            if (_hasMonoDLLsToDisplay)
            {
                GUILayout.Label(Labels.MONO_DLLS_LABEL, Settings.INFO_TITLE_STYLE_NAME);
                {
                    GUILayout.BeginHorizontal(GUILayout.MaxWidth(500));
                    DrawNames(buildReportToDisplay.MonoDLLs);
                    DrawReadableSizes(buildReportToDisplay.MonoDLLs);
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(20);
            }

            if (_hasUnityEngineDLLsToDisplay)
            {
                DrawScriptDLLsList(buildReportToDisplay, buildPlatform);
            }

            GUILayout.EndVertical();

            GUILayout.Space(15);

            // column 2
            GUILayout.BeginVertical();
            if (_hasUnityEngineDLLsToDisplay)
            {
                GUILayout.Label(Labels.UNITY_ENGINE_DLLS_LABEL, Settings.INFO_TITLE_STYLE_NAME);
                {
                    GUILayout.BeginHorizontal(GUILayout.MaxWidth(500));
                    DrawNames(buildReportToDisplay.UnityEngineDLLs);
                    DrawReadableSizes(buildReportToDisplay.UnityEngineDLLs);
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                DrawScriptDLLsList(buildReportToDisplay, buildPlatform);
            }
            GUILayout.Space(20);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawScriptDLLsList(BuildInfo buildReportToDisplay, BuildPlatform buildPlatform)
        {
            if (!_hasScriptDLLsToDisplay)
            {
                return;
            }

            GUILayout.Label(Labels.SCRIPT_DLLS_LABEL, Settings.INFO_TITLE_STYLE_NAME);
            {
                GUILayout.BeginHorizontal(GUILayout.MaxWidth(500));
                DrawNames(buildReportToDisplay.ScriptDLLs);
                DrawReadableSizes(buildReportToDisplay.ScriptDLLs);
                GUILayout.EndHorizontal();
            }
        }


        private void DrawNames(SizePart[] list)
        {
            if (list == null)
            {
                return;
            }

            GUILayout.BeginVertical();
            bool useAlt = false;
            foreach (SizePart b in list)
            {
                if (b.IsTotal)
                {
                    continue;
                }
                string styleToUse = useAlt ? Settings.LIST_NORMAL_ALT_STYLE_NAME : Settings.LIST_NORMAL_STYLE_NAME;
                GUILayout.Label(b.Name, styleToUse);
                useAlt = !useAlt;
            }
            GUILayout.EndVertical();
        }

        private void DrawReadableSizes(SizePart[] list)
        {
            if (list == null)
            {
                return;
            }

            GUILayout.BeginVertical();
            bool useAlt = false;
            foreach (SizePart b in list)
            {
                if (b.IsTotal)
                {
                    continue;
                }
                string styleToUse = useAlt ? Settings.LIST_NORMAL_ALT_STYLE_NAME : Settings.LIST_NORMAL_STYLE_NAME;
                GUILayout.Label(b.Size, styleToUse);
                useAlt = !useAlt;
            }
            GUILayout.EndVertical();
        }

        private void DrawPercentages(SizePart[] list)
        {
            if (list == null)
            {
                return;
            }

            GUILayout.BeginVertical();
            bool useAlt = false;
            foreach (SizePart b in list)
            {
                if (b.IsTotal)
                {
                    continue;
                }
                string styleToUse = useAlt ? Settings.LIST_NORMAL_ALT_STYLE_NAME : Settings.LIST_NORMAL_STYLE_NAME;
                GUILayout.Label(b.Percentage + "%", styleToUse);
                useAlt = !useAlt;
            }
            GUILayout.EndVertical();
        }
    }
}