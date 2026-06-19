using UnityEngine;
using UnityEditor;


namespace BuildReportTool.Window.Screen
{
    public class Help : BaseScreen
    {
        public override string Name => Labels.HELP_CATEGORY_LABEL;

        private const int LABEL_LENGTH = 16000;

        public override void RefreshData(BuildInfo buildReport)
        {
            const string README_FILENAME = "README.txt";
            _readmeContents = Util.GetPackageFileContents(README_FILENAME);

            const string CHANGELOG_FILENAME = "VERSION.txt";
            _changelogContents = Util.GetPackageFileContents(CHANGELOG_FILENAME);


            if (_readmeContents.Length > LABEL_LENGTH)
            {
                _readmeContents = _readmeContents.Substring(0, LABEL_LENGTH);
            }

            if (_changelogContents.Length > LABEL_LENGTH)
            {
                _changelogContents = _changelogContents.Substring(0, LABEL_LENGTH);
            }

            if (_readmeGuiContent == null)
            {
                _readmeGuiContent = new GUIContent(_readmeContents);
            }

            if (_changelogGuiContent == null)
            {
                _changelogGuiContent = new GUIContent(_changelogContents);
            }
        }

        public override void DrawGUI(Rect position, BuildInfo buildReportToDisplay)
        {
            GUI.SetNextControlName("BRT_HelpUnfocuser");
            GUI.TextField(new Rect(-100, -100, 10, 10), "");

            GUILayout.Space(10); // extra top padding

            GUILayout.BeginHorizontal();
            int newSelectedHelpIdx = GUILayout.SelectionGrid(_selectedHelpContentsIdx, _helpTypeLabels, 1);

            if (newSelectedHelpIdx != _selectedHelpContentsIdx)
            {
                GUI.FocusControl("BRT_HelpUnfocuser");
            }

            _selectedHelpContentsIdx = newSelectedHelpIdx;

            //GUILayout.Space((position.width - HELP_CONTENT_WIDTH) * 0.5f);

            if (_selectedHelpContentsIdx == HELP_TYPE_README_IDX)
            {
                _readmeScrollPos = GUILayout.BeginScrollView(_readmeScrollPos);

                float readmeHeight = GUI.skin.GetStyle(HELP_CONTENT_GUI_STYLE).CalcHeight(_readmeGuiContent, HELP_CONTENT_WIDTH);

                EditorGUILayout.SelectableLabel(_readmeContents, HELP_CONTENT_GUI_STYLE, GUILayout.Width(HELP_CONTENT_WIDTH), GUILayout.Height(readmeHeight));

                GUILayout.EndScrollView();
            }
            else if (_selectedHelpContentsIdx == HELP_TYPE_CHANGELOG_IDX)
            {
                _changelogScrollPos = GUILayout.BeginScrollView(_changelogScrollPos);

                float changelogHeight = GUI.skin.GetStyle(HELP_CONTENT_GUI_STYLE).CalcHeight(_changelogGuiContent, HELP_CONTENT_WIDTH);

                EditorGUILayout.SelectableLabel(_changelogContents, HELP_CONTENT_GUI_STYLE, GUILayout.Width(HELP_CONTENT_WIDTH), GUILayout.Height(changelogHeight));

                GUILayout.EndScrollView();
            }

            GUILayout.EndHorizontal();
        }


        private int _selectedHelpContentsIdx = 0;
        private const int HELP_TYPE_README_IDX = 0;
        private const int HELP_TYPE_CHANGELOG_IDX = 1;


        private const string HELP_CONTENT_GUI_STYLE = "label";
        private const int HELP_CONTENT_WIDTH = 500;

        private string[] _helpTypeLabels = new string[]
        {
            "Help (README)", "Version Changelog"
        };

        private Vector2 _readmeScrollPos;
        private string _readmeContents;
        private float _readmeHeight;

        private Vector2 _changelogScrollPos;
        private string _changelogContents;
        private float _changelogHeight;

        private GUIContent _readmeGuiContent;
        private GUIContent _changelogGuiContent;
    }
}