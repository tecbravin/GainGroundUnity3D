using System;
using UnityEngine;

namespace GainGround.Game
{
    public sealed partial class GainGroundGame
    {
        GainGroundTitleView titleView;

        void InitializeTitleUI()
        {
            var existingEventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            var prefab = Resources.Load<GameObject>("HUD/GainGroundTitleUI");
            if (prefab == null || prefab.GetComponent<GainGroundTitleView>() == null)
            {
                Debug.LogError("HUD/GainGroundTitleUI.prefab não encontrado em Resources.", this);
                return;
            }
            var instance = Instantiate(prefab);
            instance.name = "Gain Ground Title UI";
            if (existingEventSystem != null)
            {
                var ownEventSystem = instance.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>();
                if (ownEventSystem != null) ownEventSystem.gameObject.SetActive(false);
            }
            titleView = instance.GetComponent<GainGroundTitleView>();
            titleView.Bind(ChooseTitleButton, ToggleTitleRoster, ClickTitleGain);
        }

        void ChooseTitleButton(string label)
        {
            if (state != ScreenState.Title || !CanUseTitleMenu) return;
            menuIndex = Array.IndexOf(TitleMenuLabels, label);
            if (menuIndex >= 0) ActivateMenuItem();
        }

        void ToggleTitleRoster()
        {
            if (state != ScreenState.Title || !CanUseTitleMenu || !hasFinished ||
                settings.Difficulty != GameDifficulty.Normal || allCharacters == null ||
                allCharacters.Length == 0) return;
            useFullRoster = !useFullRoster;
        }

        void ClickTitleGain()
        {
            if (state != ScreenState.Title || !CanUseTitleMenu) return;
            CountSecretGainClick();
        }

        void RefreshTitleUI()
        {
            if (titleView == null) return;
            bool visible = state == ScreenState.Title && !saveBrowserOpen && !bindingsOpen;
            if (visible) menuIndex = Mathf.Clamp(menuIndex, 0, TitleMenuLabels.Length - 1);
            titleView.Refresh(visible, titleElapsed, titleMenuVisible, CanUseTitleMenu,
                HighScore, !HideDuoOption, HasSaves,
                hasFinished && settings.Difficulty == GameDifficulty.Normal &&
                allCharacters != null && allCharacters.Length > 0,
                useFullRoster, menuIndex);
        }
    }
}
