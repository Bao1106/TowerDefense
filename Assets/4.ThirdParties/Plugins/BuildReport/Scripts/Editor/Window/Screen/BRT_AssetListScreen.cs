using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using FuzzyString;


namespace BuildReportTool.Window.Screen
{
    public class AssetList : BaseScreen
    {
        public override string Name => "";

        public override void RefreshData(BuildInfo buildReport)
        {
            RefreshConfiguredFileFilters();
        }

        public override void DrawGUI(Rect position, BuildInfo buildReportToDisplay)
        {
            if (!buildReportToDisplay.HasUsedAssets)
            {
                return;
            }


            // init variables to use
            // --------------------------------------------------------------------------

            FileFilterGroup fileFilterGroupToUse = buildReportToDisplay.FileFilters;

            if (BuildReportTool.Options.ShouldUseConfiguredFileFilters())
            {
                fileFilterGroupToUse = _configuredFileFilterGroup;
            }

            BuildReportTool.AssetList listToDisplay = GetAssetListToDisplay(buildReportToDisplay);

            //BuildReportTool.SizePart[] assetListToUse = listToDisplay.GetListToDisplay(fileFilterGroupToUse);
            //
            //if (assetListToUse == null || assetListToUse.Length == 0)
            //{
            //	fileFilterGroupToUse.ForceSetSelectedFilterIdx(0);
            //}


            if (listToDisplay == null)
            {
                if (IsShowingUsedAssets)
                {
                    Utility.DrawCentralMessage(position, "No \"Used Assets\" included in this build report.");
                }
                else if (IsShowingUnusedAssets)
                {
                    Utility.DrawCentralMessage(position, "No \"Unused Assets\" included in this build report.");
                }
                return;
            }


            // gui
            // --------------------------------------------------------------------------

            GUILayout.Space(1);

            DrawTopBar(position, buildReportToDisplay, fileFilterGroupToUse);

            if (buildReportToDisplay.HasUsedAssets)
            {
                DrawAssetList(position, listToDisplay, fileFilterGroupToUse, BuildReportTool.Options.AssetListPaginationLength);
            }


            // status bar at bottom
            // --------------------------------------------------------------------------

            GUILayout.Space(20);

            string selectedInfoLabel = string.Format("{0}{1:N0}. {2}{3} ({4:N}%)				Click on an asset's name to include it in size calculations or batch deletions. Shift-click to select many. Ctrl-click to toggle selection.", Labels.SELECTED_QTY_LABEL, listToDisplay.GetSelectedCount(), Labels.SELECTED_SIZE_LABEL, listToDisplay.GetReadableSizeOfSumSelection(), listToDisplay.GetPercentageOfSumSelection());

            GUI.Label(new Rect(0, position.height - 20, position.width, 20), selectedInfoLabel, Settings.STATUS_BAR_LABEL_STYLE_NAME);
        }


        public enum ListToDisplay
        {
            Invalid,
            UsedAssets,
            UnusedAssets,
        }

        private ListToDisplay _currentListDisplayed = ListToDisplay.Invalid;

        public void SetListToDisplay(ListToDisplay t)
        {
            _currentListDisplayed = t;
        }

        private bool IsShowingUnusedAssets => _currentListDisplayed == ListToDisplay.UnusedAssets;

        private bool IsShowingUsedAssets => _currentListDisplayed == ListToDisplay.UsedAssets;

        private BuildReportTool.AssetList GetAssetListToDisplay(BuildInfo buildReportToDisplay)
        {
            if (_currentListDisplayed == ListToDisplay.UsedAssets)
            {
                return buildReportToDisplay.UsedAssets;
            }
            else if (_currentListDisplayed == ListToDisplay.UnusedAssets)
            {
                return buildReportToDisplay.UnusedAssets;
            }

            Debug.LogError("Invalid list to display type");
            return null;
        }


        private const int LIST_HEIGHT = 20;
        private const int ICON_DISPLAY_SIZE = 15;


        private Vector2 _assetListScrollPos;


        private FileFilterGroup _configuredFileFilterGroup = null;

        private string _searchTextInput = string.Empty;


        private void RefreshConfiguredFileFilters()
        {
            // reload used FileFilterGroup but save current used filter idx
            // to be re-set afterwards

            int tempIdx = 0;

            if (_configuredFileFilterGroup != null)
            {
                tempIdx = _configuredFileFilterGroup.GetSelectedFilterIdx();
            }

            _configuredFileFilterGroup = FiltersUsed.GetProperFileFilterGroupToUse();

            _configuredFileFilterGroup.ForceSetSelectedFilterIdx(tempIdx);
        }


        private void DrawTopBar(Rect position, BuildInfo buildReportToDisplay, FileFilterGroup fileFilterGroupToUse)
        {
            BuildReportTool.AssetList assetListUsed = GetAssetListToDisplay(buildReportToDisplay);

            if (assetListUsed == null)
            {
                return;
            }


            Texture2D prevArrow = GUI.skin.GetStyle(Settings.BIG_LEFT_ARROW_ICON_STYLE_NAME).normal.background;
            Texture2D nextArrow = GUI.skin.GetStyle(Settings.BIG_RIGHT_ARROW_ICON_STYLE_NAME).normal.background;


            GUILayout.BeginHorizontal(GUILayout.Height(11));

            GUILayout.Label(" ", Settings.TOP_BAR_BG_STYLE_NAME);

            // ------------------------------------------------------------------------------------------------------
            // File Filters

            fileFilterGroupToUse.Draw(assetListUsed, position.width - 100);

            // ------------------------------------------------------------------------------------------------------

            GUILayout.Space(20);

            // ------------------------------------------------------------------------------------------------------
            // Unused Assets Batch

            if (IsShowingUnusedAssets)
            {
                int batchNumber = buildReportToDisplay.UnusedAssetsBatchNum + 1;

                if (GUILayout.Button(prevArrow, Settings.TOP_BAR_BTN_STYLE_NAME) && batchNumber - 1 >= 1)
                {
                    // move to previous batch
                    ReportGenerator.MoveUnusedAssetsBatchToPrev(buildReportToDisplay, fileFilterGroupToUse);
                }

                string batchLabel = "Batch " + batchNumber;
                GUILayout.Label(batchLabel, Settings.TOP_BAR_LABEL_STYLE_NAME);

                if (GUILayout.Button(nextArrow, Settings.TOP_BAR_BTN_STYLE_NAME))
                {
                    // move to next batch
                    // (possible to have no new batch anymore. if so, it will just fail silently)
                    ReportGenerator.MoveUnusedAssetsBatchToNext(buildReportToDisplay, fileFilterGroupToUse);
                }
            }

            // ------------------------------------------------------------------------------------------------------

            // ------------------------------------------------------------------------------------------------------
            // Paginate Buttons

            SizePart[] assetListToUse = assetListUsed.GetListToDisplay(fileFilterGroupToUse);

            int assetListLength = 0;
            if (assetListToUse != null)
            {
                assetListLength = assetListToUse.Length;
            }

            int viewOffset = assetListUsed.GetViewOffsetForDisplayedList(fileFilterGroupToUse);

            int len = Mathf.Min(viewOffset + BuildReportTool.Options.AssetListPaginationLength, assetListLength);
            int offsetNonZeroBased = viewOffset + (len > 0 ? 1 : 0);

            string NUM_STR = "D" + assetListLength.ToString().Length;


            if (GUILayout.Button(prevArrow, Settings.TOP_BAR_BTN_STYLE_NAME) && viewOffset - BuildReportTool.Options.AssetListPaginationLength >= 0)
            {
                assetListUsed.SetViewOffsetForDisplayedList(fileFilterGroupToUse, viewOffset - BuildReportTool.Options.AssetListPaginationLength);
                _assetListScrollPos.y = 0;
            }

            string paginateLabel = "Page " + offsetNonZeroBased.ToString(NUM_STR) + " - " + len.ToString(NUM_STR) + " of " + assetListLength.ToString(NUM_STR);
            GUILayout.Label(paginateLabel, Settings.TOP_BAR_LABEL_STYLE_NAME);

            if (GUILayout.Button(nextArrow, Settings.TOP_BAR_BTN_STYLE_NAME) && viewOffset + BuildReportTool.Options.AssetListPaginationLength < assetListLength)
            {
                assetListUsed.SetViewOffsetForDisplayedList(fileFilterGroupToUse, viewOffset + BuildReportTool.Options.AssetListPaginationLength);
                _assetListScrollPos.y = 0;
            }

            // ------------------------------------------------------------------------------------------------------


            GUILayout.FlexibleSpace();

            _searchTextInput = GUILayout.TextField(_searchTextInput, "TextField-Search", GUILayout.MinWidth(200));
            if (GUILayout.Button(string.Empty, "TextField-Search-ClearButton"))
            {
                ClearSearch();
            }

            // ------------------------------------------------------------------------------------------------------
            // Recalculate Imported sizes
            // (makes sense only for unused assets)

            if (_currentListDisplayed != ListToDisplay.UsedAssets && GUILayout.Button(Labels.RECALC_IMPORTED_SIZES, Settings.TOP_BAR_BTN_STYLE_NAME))
            {
                assetListUsed.PopulateImportedSizes();
            }

            if (!BuildReportTool.Options.AutoResortAssetsWhenUnityEditorRegainsFocus && BuildReportTool.Options.GetSizeBeforeBuildForUsedAssets && _currentListDisplayed == ListToDisplay.UsedAssets && GUILayout.Button(Labels.RECALC_SIZE_BEFORE_BUILD, Settings.TOP_BAR_BTN_STYLE_NAME))
            {
                assetListUsed.PopulateSizeInAssetsFolder();
            }

            // ------------------------------------------------------------------------------------------------------


            // ------------------------------------------------------------------------------------------------------
            // Selection buttons

            if (GUILayout.Button(Labels.SELECT_ALL_LABEL, Settings.TOP_BAR_BTN_STYLE_NAME))
            {
                assetListUsed.AddAllDisplayedToSumSelection(fileFilterGroupToUse);
            }
            if (GUILayout.Button(Labels.SELECT_NONE_LABEL, Settings.TOP_BAR_BTN_STYLE_NAME))
            {
                assetListUsed.ClearSelection();
            }

            // ------------------------------------------------------------------------------------------------------


            // ------------------------------------------------------------------------------------------------------
            // Delete buttons

            if (ShouldShowDeleteButtons(buildReportToDisplay))
            {
                GUI.backgroundColor = Color.red;
                const string delSelectedLabel = "Delete selected";
                if (GUILayout.Button(delSelectedLabel, Settings.TOP_BAR_BTN_STYLE_NAME))
                {
                    InitiateDeleteSelectedUsed(buildReportToDisplay);
                }

                const string deleteAllLabel = "Delete all";
                if (GUILayout.Button(deleteAllLabel, Settings.TOP_BAR_BTN_STYLE_NAME))
                {
                    InitiateDeleteAllUnused(buildReportToDisplay);
                }

                GUI.backgroundColor = Color.white;
            }

            // ------------------------------------------------------------------------------------------------------

            GUILayout.EndHorizontal();


            GUILayout.Space(5);
        }


        private const int SCROLLBAR_BOTTOM_PADDING = 5;

        private int _lastClickedEntryIdx = -1;

        private void DrawAssetList(Rect position, BuildReportTool.AssetList list, FileFilterGroup filter, int length)
        {
            if (list != null)
            {
                if (_searchResults != null && _searchResults.Length == 0)
                {
                    DrawCentralMessage(position, "No search results");
                    return;
                }

                SizePart[] assetListToUse;

                bool hasSearchResults = _searchResults != null;

                if (hasSearchResults && _searchResults.Length > 0)
                {
                    assetListToUse = _searchResults;
                }
                else
                {
                    assetListToUse = list.GetListToDisplay(filter);
                }

                if (assetListToUse != null)
                {
                    if (assetListToUse.Length == 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        GUILayout.Label(Labels.NO_FILES_FOR_THIS_CATEGORY_LABEL, Settings.INFO_TITLE_STYLE_NAME);
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUIUtility.SetIconSize(Vector2.one * ICON_DISPLAY_SIZE);
                        bool useAlt = false;

                        int viewOffset = list.GetViewOffsetForDisplayedList(filter);

                        // if somehow view offset was out of bounds of the SizePart[] array
                        // reset it to zero
                        if (viewOffset >= assetListToUse.Length)
                        {
                            list.SetViewOffsetForDisplayedList(filter, 0);
                            viewOffset = 0;
                        }

                        int len = Mathf.Min(viewOffset + length, assetListToUse.Length);


                        GUILayout.BeginHorizontal();


                        // --------------------------------------------------------------------------------------------------------
                        // column: asset path and name
                        GUILayout.BeginVertical();
                        useAlt = false;

                        //GUILayout.Box("", BuildReportTool.Window.Settings.LIST_COLUMN_HEADER_STYLE_NAME, GUILayout.Height(LIST_HEIGHT), GUILayout.Width(position.width));

                        GUILayout.BeginHorizontal();


                        string sortTypeAssetFullPathStyleName = Settings.LIST_COLUMN_HEADER_STYLE_NAME;
                        if (!hasSearchResults && list.CurrentSortType == BuildReportTool.AssetList.SortType.AssetFullPath)
                        {
                            if (list.CurrentSortOrder == BuildReportTool.AssetList.SortOrder.Descending)
                            {
                                sortTypeAssetFullPathStyleName = Settings.LIST_COLUMN_HEADER_DESC_STYLE_NAME;
                            }
                            else
                            {
                                sortTypeAssetFullPathStyleName = Settings.LIST_COLUMN_HEADER_ASC_STYLE_NAME;
                            }
                        }
                        if (GUILayout.Button("Asset Path", sortTypeAssetFullPathStyleName, GUILayout.Height(LIST_HEIGHT)) && !hasSearchResults)
                        {
                            list.ToggleSort(BuildReportTool.AssetList.SortType.AssetFullPath);
                        }


                        string sortTypeAssetFilenameStyleName = Settings.LIST_COLUMN_HEADER_STYLE_NAME;
                        if (!hasSearchResults && list.CurrentSortType == BuildReportTool.AssetList.SortType.AssetFilename)
                        {
                            if (list.CurrentSortOrder == BuildReportTool.AssetList.SortOrder.Descending)
                            {
                                sortTypeAssetFilenameStyleName = Settings.LIST_COLUMN_HEADER_DESC_STYLE_NAME;
                            }
                            else
                            {
                                sortTypeAssetFilenameStyleName = Settings.LIST_COLUMN_HEADER_ASC_STYLE_NAME;
                            }
                        }
                        if (GUILayout.Button("Asset Filename", sortTypeAssetFilenameStyleName, GUILayout.Height(LIST_HEIGHT)) && !hasSearchResults)
                        {
                            list.ToggleSort(BuildReportTool.AssetList.SortType.AssetFilename);
                        }
                        GUILayout.EndHorizontal();


                        // --------------------------------------------------------------------------------------------------------

                        _assetListScrollPos = GUILayout.BeginScrollView(_assetListScrollPos, Settings.HIDDEN_SCROLLBAR_STYLE_NAME, Settings.HIDDEN_SCROLLBAR_STYLE_NAME);

                        for (int n = viewOffset; n < len; ++n)
                        {
                            SizePart b = assetListToUse[n];

                            string styleToUse = useAlt ? Settings.LIST_SMALL_ALT_STYLE_NAME : Settings.LIST_SMALL_STYLE_NAME;
                            bool inSumSelect = list.InSumSelection(b);
                            if (inSumSelect)
                            {
                                styleToUse = Settings.LIST_SMALL_SELECTED_NAME;
                            }

                            GUILayout.BeginHorizontal();

                            if (b.Name.StartsWith("Assets/"))
                            {
                                if (GUILayout.Button("Ping", GUILayout.Width(37)))
                                {
                                    Utility.PingAssetInProject(b.Name);
                                }
                            }
                            else
                            {
                                GUILayout.Space(37);
                            }


                            // the asset name
                            Texture icon = AssetDatabase.GetCachedIcon(b.Name);

                            string prettyName = string.Empty;

                            prettyName = string.Format(" {0}. <color=#{1}>{2}{3}</color><b>{4}</b>", n + 1, GetPathColor(inSumSelect), Util.GetAssetPath(b.Name), Util.GetAssetPathToNameSeparator(b.Name), Util.GetAssetFilename(b.Name));


                            GUIStyle styleObjToUse = GUI.skin.GetStyle(styleToUse);
                            Color temp = styleObjToUse.normal.textColor;
                            int origLeft = styleObjToUse.padding.left;
                            int origRight = styleObjToUse.padding.right;

                            styleObjToUse.normal.textColor = styleObjToUse.onNormal.textColor;
                            styleObjToUse.padding.right = 0;

                            if (icon == null)
                            {
                                //GUILayout.BeginHorizontal(styleObjToUse);
                                GUILayout.Space(24);
                                //GUILayout.EndHorizontal();
                            }

                            styleObjToUse.normal.textColor = temp;
                            styleObjToUse.padding.left = 2;
                            if (GUILayout.Button(new GUIContent(prettyName, icon), styleObjToUse, GUILayout.Height(LIST_HEIGHT)))
                            {
                                if (Event.current.control)
                                {
                                    if (!inSumSelect)
                                    {
                                        list.AddToSumSelection(b);
                                        _lastClickedEntryIdx = n;
                                    }
                                    else
                                    {
                                        list.ToggleSumSelection(b);
                                        _lastClickedEntryIdx = -1;
                                    }
                                }
                                else if (Event.current.shift)
                                {
                                    if (_lastClickedEntryIdx != -1)
                                    {
                                        // select all from last clicked to here
                                        if (_lastClickedEntryIdx < n)
                                        {
                                            for (int addToSelectIdx = _lastClickedEntryIdx; addToSelectIdx <= n; ++addToSelectIdx)
                                            {
                                                list.AddToSumSelection(assetListToUse[addToSelectIdx]);
                                            }
                                        }
                                        else if (_lastClickedEntryIdx > n)
                                        {
                                            for (int addToSelectIdx = n; addToSelectIdx <= _lastClickedEntryIdx; ++addToSelectIdx)
                                            {
                                                list.AddToSumSelection(assetListToUse[addToSelectIdx]);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        list.AddToSumSelection(b);
                                    }

                                    _lastClickedEntryIdx = n;
                                }
                                else
                                {
                                    list.ClearSelection();
                                    list.AddToSumSelection(b);
                                    _lastClickedEntryIdx = n;
                                }
                            }
                            styleObjToUse.padding.right = origRight;
                            styleObjToUse.padding.left = origLeft;


                            GUILayout.EndHorizontal();

                            useAlt = !useAlt;
                        }


                        GUILayout.Space(SCROLLBAR_BOTTOM_PADDING);

                        GUILayout.EndScrollView();

                        GUILayout.EndVertical();


                        bool pressedRawSizeSortBtn = false;
                        bool pressedImpSizeSortBtn = false;

                        bool pressedSizeBeforeBuildSortBtn = false;

                        // --------------------------------------------------------------------------------------------------------
                        // column: raw file size


                        if (IsShowingUsedAssets && assetListToUse[0].SizeInAssetsFolderBytes != -1)
                        {
                            pressedSizeBeforeBuildSortBtn = DrawColumn(viewOffset, len, BuildReportTool.AssetList.SortType.SizeBeforeBuild, "Size Before Build   ", !hasSearchResults, false, list, assetListToUse, (b) => b.SizeInAssetsFolder, ref _assetListScrollPos);
                        }

                        if (IsShowingUsedAssets && BuildReportTool.Options.ShowImportedSizeForUsedAssets)
                        {
                            pressedRawSizeSortBtn = DrawColumn(viewOffset, len, BuildReportTool.AssetList.SortType.ImportedSizeOrRawSize, "Size In Build", !hasSearchResults, false, list, assetListToUse, (b) =>
                            {
                                // assets in the "StreamingAssets" folder do not have an imported size
                                // in those cases, the raw size is the same as the imported size
                                // so just use the raw size
                                if (b.ImportedSize == "N/A")
                                {
                                    return b.RawSize;
                                }

                                return b.ImportedSize;
                            }, ref _assetListScrollPos);
                        }

                        if (IsShowingUnusedAssets || IsShowingUsedAssets && !BuildReportTool.Options.ShowImportedSizeForUsedAssets)
                        {
                            pressedRawSizeSortBtn = DrawColumn(viewOffset, len, BuildReportTool.AssetList.SortType.RawSize, IsShowingUnusedAssets ? "Raw Size" : "Size In Build", !hasSearchResults, false, list, assetListToUse, (b) => b.RawSize, ref _assetListScrollPos);
                        }


                        bool showScrollbarForImportedSize = IsShowingUnusedAssets;


                        // --------------------------------------------------------------------------------------------------------
                        // column: imported file size


                        if (IsShowingUnusedAssets)
                        {
                            pressedImpSizeSortBtn = DrawColumn(viewOffset, len, BuildReportTool.AssetList.SortType.ImportedSize, "Imported Size   ", !hasSearchResults, showScrollbarForImportedSize, list, assetListToUse, (b) => b.ImportedSize, ref _assetListScrollPos);
                        }


                        // --------------------------------------------------------------------------------------------------------
                        // column: percentage to total size

                        bool pressedPercentSortBtn = false;

                        if (IsShowingUsedAssets)
                        {
                            pressedPercentSortBtn = DrawColumn(viewOffset, len, BuildReportTool.AssetList.SortType.PercentSize, "Percent   ", !hasSearchResults, true, list, assetListToUse, (b) =>
                            {
                                string text = b.Percentage + "%";
                                if (b.Percentage < 0)
                                {
                                    text = Labels.NON_APPLICABLE_PERCENTAGE_LABEL;
                                }
                                return text;
                            }, ref _assetListScrollPos);
                        }

                        // --------------------------------------------------------------------------------------------------------

                        if (!hasSearchResults)
                        {
                            if (pressedRawSizeSortBtn)
                            {
                                BuildReportTool.AssetList.SortType sortType = BuildReportTool.AssetList.SortType.RawSize;
                                if (IsShowingUsedAssets && BuildReportTool.Options.ShowImportedSizeForUsedAssets)
                                {
                                    sortType = BuildReportTool.AssetList.SortType.ImportedSizeOrRawSize;
                                }
                                list.ToggleSort(sortType);
                            }
                            else if (pressedSizeBeforeBuildSortBtn)
                            {
                                list.ToggleSort(BuildReportTool.AssetList.SortType.SizeBeforeBuild);
                            }
                            else if (pressedImpSizeSortBtn)
                            {
                                list.ToggleSort(BuildReportTool.AssetList.SortType.ImportedSize);
                            }
                            else if (pressedPercentSortBtn)
                            {
                                list.ToggleSort(BuildReportTool.AssetList.SortType.PercentSize);
                            }
                        }


                        GUILayout.EndHorizontal();
                    }
                }
            }
        }

        private string GetPathColor(bool inSumSelect)
        {
            const string pathColorUnselectedWhiteSkin = "4f4f4fff";
            const string pathColorSelectedWhiteSkin = "cececeff";

            const string pathColorUnselectedDarkSkin = "767676ff";
            const string pathColorSelectedDarkSkin = "c2c2c2ff";

            string colorUsedForPath = pathColorUnselectedWhiteSkin;
            if (EditorGUIUtility.isProSkin)
            {
                colorUsedForPath = pathColorUnselectedDarkSkin;
            }

            if (inSumSelect)
            {
                if (EditorGUIUtility.isProSkin)
                {
                    colorUsedForPath = pathColorSelectedDarkSkin;
                }
                else
                {
                    colorUsedForPath = pathColorSelectedWhiteSkin;
                }
            }

            return colorUsedForPath;
        }


        private delegate string ColumnDisplayDelegate(SizePart b);

        private bool DrawColumn(int sta, int end, BuildReportTool.AssetList.SortType columnType, string columnName, bool allowSort, bool showScrollbar, BuildReportTool.AssetList assetListCollection, SizePart[] assetList, ColumnDisplayDelegate dataToDisplay, ref Vector2 scollbarPos)
        {
            bool buttonPressed = false;
            GUILayout.BeginVertical();


            // ----------------------------------------------------------
            // column header
            string sortTypeStyleName = Settings.LIST_COLUMN_HEADER_STYLE_NAME;
            if (allowSort && assetListCollection.CurrentSortType == columnType)
            {
                if (assetListCollection.CurrentSortOrder == BuildReportTool.AssetList.SortOrder.Descending)
                {
                    sortTypeStyleName = Settings.LIST_COLUMN_HEADER_DESC_STYLE_NAME;
                }
                else
                {
                    sortTypeStyleName = Settings.LIST_COLUMN_HEADER_ASC_STYLE_NAME;
                }
            }
            if (GUILayout.Button(columnName, sortTypeStyleName, GUILayout.Height(LIST_HEIGHT)) && allowSort)
            {
                buttonPressed = true;
            }


            // ----------------------------------------------------------
            // scrollbar
            if (showScrollbar)
            {
                scollbarPos = GUILayout.BeginScrollView(scollbarPos, Settings.HIDDEN_SCROLLBAR_STYLE_NAME, "verticalscrollbar");
            }
            else
            {
                scollbarPos = GUILayout.BeginScrollView(scollbarPos, Settings.HIDDEN_SCROLLBAR_STYLE_NAME, Settings.HIDDEN_SCROLLBAR_STYLE_NAME);
            }


            // ----------------------------------------------------------
            // actual contents
            bool useAlt = false;

            SizePart b;
            for (int n = sta; n < end; ++n)
            {
                b = assetList[n];

                string styleToUse = useAlt ? Settings.LIST_SMALL_ALT_STYLE_NAME : Settings.LIST_SMALL_STYLE_NAME;
                if (assetListCollection.InSumSelection(b))
                {
                    styleToUse = Settings.LIST_SMALL_SELECTED_NAME;
                }

                GUILayout.Label(dataToDisplay(b), styleToUse, GUILayout.MinWidth(90), GUILayout.Height(LIST_HEIGHT));

                useAlt = !useAlt;
            }

            GUILayout.Space(SCROLLBAR_BOTTOM_PADDING);

            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            return buttonPressed;
        }


        private bool ShouldShowDeleteButtons(BuildInfo buildReportToDisplay)
        {
            return IsShowingUnusedAssets && buildReportToDisplay.HasUnusedAssets || buildReportToDisplay.HasUsedAssets && BuildReportTool.Options.AllowDeletingOfUsedAssets;
        }


        private void InitiateDeleteSelectedUsed(BuildInfo buildReportToDisplay)
        {
            BuildReportTool.AssetList listToDeleteFrom = GetAssetListToDisplay(buildReportToDisplay);

            InitiateDeleteSelectedInAssetList(buildReportToDisplay, listToDeleteFrom);
        }

        private void InitiateDeleteSelectedInAssetList(BuildInfo buildReportToDisplay, BuildReportTool.AssetList listToDeleteFrom)
        {
            if (listToDeleteFrom.IsNothingSelected)
            {
                return;
            }


            SizePart[] all = listToDeleteFrom.All;

            int numOfFilesRequestedToDelete = listToDeleteFrom.GetSelectedCount();
            int numOfFilesToDelete = numOfFilesRequestedToDelete;
            int systemDeletionFileCount = 0;
            int brtFilesSelectedForDelete = 0;


            // filter out files that shouldn't be deleted
            // and identify unrecoverable files
            for (int n = 0, len = all.Length; n < len; ++n)
            {
                SizePart b = all[n];
                bool isThisFileToBeDeleted = listToDeleteFrom.InSumSelection(b);

                if (isThisFileToBeDeleted)
                {
                    if (Util.IsFileInBuildReportFolder(b.Name) && !Util.IsUselessFile(b.Name))
                    {
                        //Debug.Log("BRT file? " + b.Name);
                        --numOfFilesToDelete;
                        ++brtFilesSelectedForDelete;
                    }
                    else if (Util.HaveToUseSystemForDelete(b.Name))
                    {
                        ++systemDeletionFileCount;
                    }
                }
            }

            if (numOfFilesToDelete <= 0)
            {
                if (brtFilesSelectedForDelete > 0)
                {
                    EditorApplication.Beep();
                    EditorUtility.DisplayDialog("Can't delete!", "Take note that for safety, Build Report Tool assets themselves will not be included for deletion.", "OK");
                }
                return;
            }


            // prepare warning message for user

            bool deletingSystemFilesOnly = systemDeletionFileCount == numOfFilesToDelete;
            bool deleteIsRecoverable = !deletingSystemFilesOnly;

            string plural = "";
            if (numOfFilesToDelete > 1)
            {
                plural = "s";
            }

            string message = null;

            if (numOfFilesRequestedToDelete != numOfFilesToDelete)
            {
                message = "Among " + numOfFilesRequestedToDelete + " file" + plural + " requested to be deleted, only " + numOfFilesToDelete + " will be deleted.";
            }
            else
            {
                message = "This will delete " + numOfFilesToDelete + " asset" + plural + " in your project.";
            }

            // add warning about BRT files that are skipped
            if (brtFilesSelectedForDelete > 0)
            {
                message += "\n\nTake note that for safety, " + brtFilesSelectedForDelete + " file" + (brtFilesSelectedForDelete > 1 ? "s" : "") + " found to be Build Report Tool assets are not included for deletion.";
            }

            // add warning about unrecoverable files
            if (systemDeletionFileCount > 0)
            {
                if (deletingSystemFilesOnly)
                {
                    message += "\n\nThe deleted file" + plural + " will not be recoverable from the " + Util.NameOfOSTrashFolder + ", unless you have your own backup.";
                }
                else
                {
                    message += "\n\nAmong the " + numOfFilesToDelete + " file" + plural + " for deletion, " + systemDeletionFileCount + " will not be recoverable from the " + Util.NameOfOSTrashFolder + ", unless you have your own backup.";
                }
                message += "\n\nThis is a limitation in Unity and .NET code. To ensure deleting will move the files to the " + Util.NameOfOSTrashFolder + " instead, delete your files the usual way using your project view.";
            }
            else
            {
                message += "\n\nThe deleted file" + plural + " can be recovered from your " + Util.NameOfOSTrashFolder + ".";
            }

            message += "\n\nDeleting a large number of files may take a long time as Unity will rebuild the project's Library folder.\n\nProceed with deleting?";

            EditorApplication.Beep();
            if (!EditorUtility.DisplayDialog("Delete?", message, "Yes", "No"))
            {
                return;
            }

            List<SizePart> allList = new List<SizePart>(all);
            List<SizePart> toRemove = new List<SizePart>(all.Length / 4);

            // finally, delete the files
            int deletedCount = 0;
            for (int n = 0, len = allList.Count; n < len; ++n)
            {
                SizePart b = allList[n];


                bool okToDelete = Util.IsUselessFile(b.Name) || !Util.IsFileInBuildReportFolder(b.Name);

                if (listToDeleteFrom.InSumSelection(b) && okToDelete)
                {
                    // delete this

                    if (Util.ShowFileDeleteProgress(deletedCount, numOfFilesToDelete, b.Name, deleteIsRecoverable))
                    {
                        return;
                    }

                    Util.DeleteSizePartFile(b);
                    toRemove.Add(b);
                    ++deletedCount;
                }
            }
            EditorUtility.ClearProgressBar();


            // refresh the asset lists
            allList.RemoveAll(i => toRemove.Contains(i));
            SizePart[] allWithRemoved = allList.ToArray();

            // recreate per category list (maybe just remove from existing per category lists instead?)
            SizePart[][] perCategoryOfList = ReportGenerator.SegregateAssetSizesPerCategory(allWithRemoved, buildReportToDisplay.FileFilters);

            listToDeleteFrom.Reinit(allWithRemoved, perCategoryOfList, IsShowingUsedAssets ? BuildReportTool.Options.NumberOfTopLargestUsedAssetsToShow : BuildReportTool.Options.NumberOfTopLargestUnusedAssetsToShow);
            listToDeleteFrom.ClearSelection();


            // print info about the delete operation to console
            string finalMessage = deletedCount + " file" + plural + " removed from your project.";
            if (deleteIsRecoverable)
            {
                finalMessage += " They can be recovered from your " + Util.NameOfOSTrashFolder + ".";
            }

            EditorApplication.Beep();
            EditorUtility.DisplayDialog("Delete successful", finalMessage, "OK");

            Debug.LogWarning(finalMessage);
        }


        private void InitiateDeleteAllUnused(BuildInfo buildReportToDisplay)
        {
            BuildReportTool.AssetList list = buildReportToDisplay.UnusedAssets;
            SizePart[] all = list.All;

            int filesToDeleteCount = 0;

            for (int n = 0, len = all.Length; n < len; ++n)
            {
                SizePart b = all[n];

                bool okToDelete = Util.IsFileOkForDeleteAllOperation(b.Name);

                if (okToDelete)
                {
                    //Debug.Log("added " + b.Name + " for deletion");
                    ++filesToDeleteCount;
                }
            }

            if (filesToDeleteCount == 0)
            {
                const string nothingToDelete = "Take note that for safety, Build Report Tool assets, Unity editor assets, version control metadata, and Unix-style hidden files will not be included for deletion.\n\nYou can force deleting them by selecting them (via the checkbox) and using \"Delete selected\", or simply delete them the normal way in your project view.";

                EditorApplication.Beep();
                EditorUtility.DisplayDialog("Nothing to delete!", nothingToDelete, "Ok");
                return;
            }

            string plural = "";
            if (filesToDeleteCount > 1)
            {
                plural = "s";
            }

            EditorApplication.Beep();
            if (!EditorUtility.DisplayDialog("Delete?", "Among " + all.Length + " file" + plural + " in your project, " + filesToDeleteCount + " will be deleted.\n\nBuild Report Tool assets themselves, Unity editor assets, version control metadata, and Unix-style hidden files will not be included for deletion. You can force-delete those by selecting them (via the checkbox) and use \"Delete selected\", or simply delete them the normal way in your project view.\n\nDeleting a large number of files may take a long time as Unity will rebuild the project's Library folder.\n\nAre you sure about this?\n\nThe file" + plural + " can be recovered from your " + Util.NameOfOSTrashFolder + ".", "Yes", "No"))
            {
                return;
            }

            List<SizePart> newAll = new List<SizePart>();

            int deletedCount = 0;
            for (int n = 0, len = all.Length; n < len; ++n)
            {
                SizePart b = all[n];

                bool okToDelete = Util.IsFileOkForDeleteAllOperation(b.Name);

                if (okToDelete)
                {
                    // delete this
                    if (Util.ShowFileDeleteProgress(deletedCount, filesToDeleteCount, b.Name, true))
                    {
                        return;
                    }

                    Util.DeleteSizePartFile(b);
                    ++deletedCount;
                }
                else
                {
                    //Debug.Log("added " + b.Name + " to new list");
                    newAll.Add(b);
                }
            }
            EditorUtility.ClearProgressBar();

            SizePart[] newAllArr = newAll.ToArray();

            SizePart[][] perCategoryUnused = ReportGenerator.SegregateAssetSizesPerCategory(newAllArr, buildReportToDisplay.FileFilters);

            list.Reinit(newAllArr, perCategoryUnused, IsShowingUsedAssets ? BuildReportTool.Options.NumberOfTopLargestUsedAssetsToShow : BuildReportTool.Options.NumberOfTopLargestUnusedAssetsToShow);
            list.ClearSelection();


            string finalMessage = filesToDeleteCount + " file" + plural + " removed from your project. They can be recovered from your " + Util.NameOfOSTrashFolder + ".";
            Debug.LogWarning(finalMessage);

            EditorApplication.Beep();
            EditorUtility.DisplayDialog("Delete successful", finalMessage, "OK");
        }


        private const double SEARCH_DELAY = 0.25f;
        private double _lastSearchTime;
        private string _lastSearchText = string.Empty;

        private void ClearSearch()
        {
            _searchTextInput = string.Empty;
            _lastSearchText = string.Empty;
            _searchResults = null;
        }

        public override void Update(double timeNow, double deltaTime, BuildInfo buildReportToDisplay)
        {
            if (string.IsNullOrEmpty(_searchTextInput))
            {
                // cancel search
                ClearSearch();
                if (buildReportToDisplay != null)
                {
                    buildReportToDisplay.FlagOkToRefresh();
                }
            }
            else if (timeNow - _lastSearchTime >= SEARCH_DELAY && _lastSearchText != _searchTextInput)
            {
                // update search
                _lastSearchText = _searchTextInput;
                _lastSearchTime = EditorApplication.timeSinceStartup;

                if (buildReportToDisplay != null)
                {
                    Search(_lastSearchText, buildReportToDisplay);
                    buildReportToDisplay.FlagOkToRefresh();
                }

                _lastSearchTime = timeNow;
            }
        }

        private SizePart[] _searchResults;

        private void Search(string searchText, BuildInfo buildReportToDisplay)
        {
            BuildReportTool.AssetList list = GetAssetListToDisplay(buildReportToDisplay);


            FileFilterGroup filter = buildReportToDisplay.FileFilters;

            if (BuildReportTool.Options.ShouldUseConfiguredFileFilters())
            {
                filter = _configuredFileFilterGroup;
            }

            List<SizePart> searchResults = new List<SizePart>();


            SizePart[] assetListToSearchFrom = list.GetListToDisplay(filter);

            for (int n = 0; n < assetListToSearchFrom.Length; ++n)
            {
                if (IsANearStringMatch(assetListToSearchFrom[n].Name, searchText))
                {
                    searchResults.Add(assetListToSearchFrom[n]);
                }
            }

            if (searchResults.Count > 0)
            {
                searchResults.Sort((a, b) => GetFuzzyEqualityScore(searchText, a.Name).CompareTo(GetFuzzyEqualityScore(searchText, b.Name)));
            }

            _searchResults = searchResults.ToArray();
        }


        // Search algorithms that will weigh in for the comparison
        private readonly FuzzyStringComparisonOptions[] _searchOptions =
        {
            FuzzyStringComparisonOptions.UseOverlapCoefficient, FuzzyStringComparisonOptions.UseLongestCommonSubsequence, FuzzyStringComparisonOptions.UseLongestCommonSubstring
        };

        private bool IsANearStringMatch(string source, string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return false;
            }

            // Choose the relative strength of the comparison - is it almost exactly equal? or is it just close?
            const FuzzyStringComparisonTolerance TOLERANCE = FuzzyStringComparisonTolerance.Strong;

            // Get a boolean determination of approximate equality
            return source.ApproximatelyEquals(target, TOLERANCE, _searchOptions);
        }

        private double GetFuzzyEqualityScore(string source, string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return 0;
            }

            return source.GetFuzzyEqualityScore(target, _searchOptions);
        }
    }
}