using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TDStageCardView : MonoBehaviour
{
    // Prefab structure:
    // StagePrefab (root) ── ProceduralImage (gold border) + Button
    // ├── Outline ── ProceduralImage (dark bg fill)
    // ├── TxtStageNum ── TMP_Text
    // ├── StageInfo
    // │ ├── TxtStageTitle ── TMP_Text (DisplayName)
    // │ └── TxtStageName ── TMP_Text (StageId sub-info)
    // ├── Selected ── GameObject (badge, show when selected)
    // └── IconLock ── GameObject (lock icon)

    [SerializeField] private TMP_Text m_TxtNumber;
    [SerializeField] private TMP_Text m_TxtTitle;
    [SerializeField] private TMP_Text m_TxtSubInfo;
    [SerializeField] private GameObject m_SelectedGroup;
    [SerializeField] private GameObject m_LockIcon;
    [SerializeField] private Button m_Button;

    public event Action<TDStageConfig> OnSelected;
    public TDStageConfig Stage => m_Stage;

    private TDStageConfig m_Stage;
    private Graphic m_RootGraphic;
    private Tween m_BorderTween;

    private void Awake()
    {
        m_RootGraphic = GetComponent<Graphic>();
    }

    public void Setup(TDStageConfig stage)
    {
        m_Stage = stage;

        bool locked = stage.isLocked;
        m_TxtNumber.text = $"{(stage.LevelIndex + 1):D2}";
        m_TxtTitle.text = locked ? "???" : stage.DisplayName;
        m_TxtSubInfo.text = locked ? "COMING SOON" : stage.StageId;

        m_LockIcon?.SetActive(locked);
        m_Button.interactable = !locked;

        SetSelected(false, animate: false);
        m_Button.onClick.RemoveAllListeners();
        m_Button.onClick.AddListener(() => OnSelected?.Invoke(m_Stage));
    }

    public void SetSelected(bool selected, bool animate = true)
    {
        bool locked = m_Stage != null && m_Stage.isLocked;

        // Locked cards: fixed dim state, never show selected badge
        if (locked)
        {
            if (m_SelectedGroup != null) m_SelectedGroup.SetActive(false);
            if (m_RootGraphic == null) return;
            m_BorderTween?.Kill();
            if (animate)
                m_BorderTween = m_RootGraphic.DOFade(TDConstant.STAGE_CARD_LOCKED_ALPHA, 0.18f).SetUpdate(true);
            else
            {
                var lc = m_RootGraphic.color; lc.a = TDConstant.STAGE_CARD_LOCKED_ALPHA; m_RootGraphic.color = lc;
            }
            return;
        }

        if (m_SelectedGroup != null) m_SelectedGroup.SetActive(selected);
        if (m_RootGraphic == null) return;
        m_BorderTween?.Kill();

        float targetAlpha = selected ? 1f : 0.15f;
        if (animate)
            m_BorderTween = m_RootGraphic.DOFade(targetAlpha, 0.18f).SetUpdate(true);
        else
        {
            var c = m_RootGraphic.color; c.a = targetAlpha; m_RootGraphic.color = c;
        }
    }

    private void OnDestroy() => m_BorderTween?.Kill();
}
