using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        GainGroundStageSelectView stageSelectView;

        void InitializeStageSelectUI()
        {
            var existingEventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            var prefab = Resources.Load<GameObject>("HUD/GainGroundStageSelectUI");
            if (prefab == null || prefab.GetComponent<GainGroundStageSelectView>() == null)
            {
                Debug.LogError("HUD/GainGroundStageSelectUI.prefab não encontrado em Resources.", this);
                return;
            }
            var instance = Instantiate(prefab);
            instance.name = "Gain Ground Stage Selection UI";
            if (existingEventSystem != null)
            {
                var ownEventSystem = instance.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>();
                if (ownEventSystem != null) ownEventSystem.gameObject.SetActive(false);
            }
            stageSelectView = instance.GetComponent<GainGroundStageSelectView>();
            stageSelectView.Bind(ChangeSelectedStage, StartSelectedStage, BackFromStageSelection);
            stageSelectView.Refresh(false, null, 0, false, "");
        }

        void ChangeSelectedStage(int delta)
        {
            if (state != ScreenState.StageSelect || stages == null || stages.Length < 2) return;
            stageSelectIndex = (stageSelectIndex + delta + stages.Length) % stages.Length;
        }

        void StartSelectedStage()
        {
            if (state == ScreenState.StageSelect && stages != null && stages.Length > 0)
                StartAtSelectedStage(stageSelectIndex);
        }

        void BackFromStageSelection()
        {
            if (state == ScreenState.StageSelect)
            {
                stageIntroRequested = false;
                state = ScreenState.Title;
            }
        }

        void RefreshStageSelectUI()
        {
            if (stageSelectView == null) return;
            stageSelectView.Refresh(state == ScreenState.StageSelect && !saveBrowserOpen && !bindingsOpen,
                stages, stageSelectIndex, streamStageMedia, mediaError);
        }
    }
}
