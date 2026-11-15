using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreensaverScreen : ScreenBase
{
    [SerializeField] private Button _touchArea;

    public override void Initialize()
    {
        _touchArea.onClick.AddListener(OnScreenTapped);
        _touchArea.gameObject.SetActive(true);
    }

    public override IEnumerator AnimateShow()
    {
        ScreenManager.Instance.GetScreen<PhotoReviewScreen>().CleanPhotosContainer();
        GlobalChosesDataContainer.Instance.Clean();
        ScreenManager.Instance.GetScreen<LightingModeSelectionScreen>().SelectOption(4);
        yield return AnimateFadeIn(_canvasGroup, _fadeDuration);
        yield return Resources.UnloadUnusedAssets();
    }

    public override IEnumerator AnimateHide()
    {
        yield return AnimateFadeOut(_canvasGroup, _fadeDuration);
    }

    private void OnScreenTapped()
    {
        ScreenManager.Instance.ShowScreen<ShootingModeSelectionScreen>(() =>
        {
            Debug.Log("Transition to mode selection complete");
        });
    }
}
