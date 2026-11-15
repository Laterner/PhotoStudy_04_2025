using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

public class PhotoCaptureScreen : ScreenBase
{
    [SerializeField] private Text _countdownText;
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _captureButton;
    [SerializeField] private Image _screenImage;
    [SerializeField] private Loader _loader;

    [SerializeField] private float _countdownDuration = 10f;
    [SerializeField] private int _photosToTake = 8;
    [SerializeField] private float _photoInterval = 0.5f;
    [SerializeField] private string cameraAppAddress = "http://127.0.0.1:8080";
    [SerializeField] private string cameraAppShotCount = "12";

    private bool _isCapturing, _shotingStarted;
    private Coroutine _captureRoutine;

    public override void Initialize()
    {
        DirectoryInfo d = new DirectoryInfo(Application.streamingAssetsPath);
        var files = d.GetFiles("*.txt");
        if (files == null)
        {
            throw new System.Exception("NO CONFIGS IN STREAMING ASSETS");
        }
        try
        {
            var cameraAppCongig = files.First((x) => x.Name.Contains("cameraApp"));
            var sr = cameraAppCongig.OpenText();
            var cameraAppData = sr.ReadToEnd();
            var splitPathes = cameraAppData.Split(new char[] { '|' });
            cameraAppAddress = splitPathes[0];
            cameraAppShotCount = splitPathes[1];
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
        }
        _backButton.onClick.RemoveAllListeners();
        _backButton.onClick.AddListener(OnBackPressed);
        _captureButton.onClick.RemoveAllListeners();
        _captureButton.onClick.AddListener(StartCapture);
    }

    public override IEnumerator AnimateShow()
    {
        SetupCaptureScreen();
        yield return AnimateFadeIn(_canvasGroup, _fadeDuration);
    }

    public override IEnumerator AnimateHide()
    {
        yield return AnimateFadeOut(_canvasGroup, _fadeDuration);
    }

    private void SetupCaptureScreen()
    {
        _screenImage.sprite = GlobalChosesDataContainer.Instance.LightingMode.diagramSprite;
        _countdownText.gameObject.SetActive(false);
        _isCapturing = false;
    }

    public void StartCapture()
    {
        if (!_isCapturing)
        {
            _isCapturing = true;
            _loader.StopMonitoring();
            _loader.CleanFolder();


            ScreenManager.Instance.GetScreen<PhotoReviewScreen>().CleanPhotosContainer();
            _loader.StartMonitoring();
            _captureRoutine = StartCoroutine(CaptureRoutine());
        }
    }

    private IEnumerator CaptureRoutine()
    {
        _isCapturing = true;
        if (GlobalChosesDataContainer.Instance.ShootingMode == ShootingModeSelectionScreen.ShootingMode.SingleParticipant)
        {
            _countdownText.gameObject.SetActive(true);

            float timer = _countdownDuration;
            while (timer > 0)
            {
                _countdownText.text = Mathf.CeilToInt(timer).ToString();
                timer -= Time.deltaTime;
                yield return null;
            }
        }

#if !UNITY_EDITOR
        UnityWebRequestAsyncOperation operation = null;
        try
        {
            _captureRequest = UnityWebRequest.Get(cameraAppAddress + $"/camera?action=multishot&count={cameraAppShotCount}");
            _captureRequest.timeout = 30;
            operation = _captureRequest.SendWebRequest();
        }
        catch (System.Exception e) { Debug.LogWarning(e.Message); }
        if (operation != null) yield return operation;
        bool success = operation != null && _captureRequest != null &&
            _captureRequest.result == UnityWebRequest.Result.Success;
        DisposeCaptureRequest();
        if (!success)
        {
            _countdownText.gameObject.SetActive(false);
            _isCapturing = false;
            ScreenManager.Instance.EnableError();
            yield break;
        }
#endif
        ScreenManager.Instance.ShowScreen<PhotoReviewScreen>();
    }

    private UnityWebRequest _captureRequest;
    private void DisposeCaptureRequest()
    {
        _captureRequest?.Dispose();
        _captureRequest = null;
    }
    private void OnDisable() { DisposeCaptureRequest(); }
    private void OnBackPressed()
    {
        DisposeCaptureRequest();
        if (_isCapturing)
        {
            if (_captureRoutine != null)
                StopCoroutine(_captureRoutine);
            _isCapturing = false;
        }
        //ScreenManager.Instance.ShowPreviousScreen();
        ScreenManager.Instance.ShowScreen<LightingModeSelectionScreen>();
    }
}
