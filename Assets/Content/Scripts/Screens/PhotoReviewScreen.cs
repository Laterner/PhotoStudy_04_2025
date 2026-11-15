using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PhotoReviewScreen : ScreenBase
{
    [SerializeField] private Transform _photosContainer;
    [SerializeField] private GameObject _photoPrefab;

    [SerializeField] private Button _downloadButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private Loader _loader;
    private bool _canSelect;
    private readonly List<GameObject> _thumbnails = new List<GameObject>();

    private List<Texture2D> CapturedPhotos => GlobalChosesDataContainer.Instance.Photos;
    private List<int> _selectedPhotoIndices = new List<int>();


    public bool TESTMODE;

    public override void Initialize()
    {
        CleanPhotosContainer();
        _downloadButton.onClick.AddListener(OnDownloadPressed);
        _backButton.onClick.AddListener(OnBackPressed);
        _loader.OnNewPhotoLoaded -= LoadCapturedPhotos;
        _loader.OnAllPhotosLoaded -= HandleAllPhotosLoaded;
        _loader.OnNewPhotoLoaded += LoadCapturedPhotos;
        _loader.OnAllPhotosLoaded += HandleAllPhotosLoaded;
    }

    public override IEnumerator AnimateShow()
    {
        yield return AnimateFadeIn(_canvasGroup, _fadeDuration);
    }
    public override IEnumerator AnimateHide()
    {
        yield return AnimateFadeOut(_canvasGroup, _fadeDuration);
    }
    private void LoadCapturedPhotos(Texture2D tex)
    {
        CapturedPhotos.Add(tex);
        Debug.Log(CapturedPhotos.Count);

        DisplayOnePhoto(tex, CapturedPhotos.Count - 1);
        if (CapturedPhotos.Count > 0)
        {
            _canSelect = true;
        }
    }

    private void DisplayOnePhoto(Texture2D tex, int index)
    {
        var photoObj = Instantiate(_photoPrefab, _photosContainer);
        _thumbnails.Add(photoObj);
        var image = photoObj.transform.Find("Photo").GetComponent<Image>();
        var button = photoObj.GetComponent<Button>();

        image.gameObject.GetComponent<SmartImageFitter>().SetImage(TextureConverter.ConvertTextureToSprite(tex));

        button.onClick.AddListener(() => TogglePhotoSelection(index));

        UpdatePhotoSelectionVisual(photoObj, index);
    }

    private void HandleAllPhotosLoaded()
    {
        _canSelect = true;
    }

    private void TogglePhotoSelection(int index)
    {
        if (!_canSelect)
        {
            return;
        }
        if (_selectedPhotoIndices.Contains(index))
        {
            _selectedPhotoIndices.Remove(index);
        }
        else
        {
            _selectedPhotoIndices.Add(index);
        }

        var photoObj = _thumbnails[index];
        UpdatePhotoSelectionVisual(photoObj, index);
    }

    private void UpdatePhotoSelectionVisual(GameObject photoObj, int index)
    {
        var frame = photoObj.transform.Find("Frame");
        if (frame != null)
        {
            frame.gameObject.SetActive(_selectedPhotoIndices.Contains(index));
        }
    }

    public void CleanPhotosContainer()
    {
        foreach (var thumbnail in _thumbnails) Destroy(thumbnail);
        _thumbnails.Clear();
        GlobalChosesDataContainer.Instance.ClearPhotos();
        _canSelect = false;
        _selectedPhotoIndices = new List<int>();
    }

    private async void OnDownloadPressed()
    {
        if (_selectedPhotoIndices.Count == 0) return;
        _canSelect = false;
        var selectedPhotos = CapturedPhotos.Where(photo => _selectedPhotoIndices.Contains(CapturedPhotos.IndexOf(photo)));
        GlobalChosesDataContainer.Instance.SelectedPhotos = selectedPhotos.ToList();
        ScreenManager.Instance.GetScreen<QrCodeScreen>().SetSelectedPhotos(selectedPhotos.ToList());
        _canSelect = true;
        // await _loader.UploadSelectedPhotosToDisk(selectedPhotos.ToArray(), true);                                                
        ScreenManager.Instance.ShowScreen<QrCodeScreen>();
    }

    private void OnBackPressed()
    {
        GlobalChosesDataContainer.Instance.SelectedPhotos = new List<Texture2D>();

        if (!TESTMODE)
            _loader.CleanFolder();
        //ScreenManager.Instance.ShowPreviousScreen();
        ScreenManager.Instance.ShowScreen<PhotoCaptureScreen>();
    }

}
