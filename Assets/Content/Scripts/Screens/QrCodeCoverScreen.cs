using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using Unity.VisualScripting;
using DG.Tweening;
using System.Threading.Tasks;

public class QrCodeCoverScreen : ScreenBase
{
    [SerializeField] private Transform _photosContainer;
    [SerializeField] private Image _posterImage;
    [SerializeField] private Button _toMainScreenButton;
    [SerializeField] private Button _checkDisksButton;
    [SerializeField] private Loader _loader;
    [SerializeField] private CanvasGroup _qrCodeImagePreloaderCG, _posterImagePreloaderCG;
    [SerializeField] private Image _qrCodeImage;
    private List<Texture2D> _selectedPhotos = new List<Texture2D>();
    private bool _photosDownloaded;
    private bool _canUpload;
    public Image diskAvaliableIcon;

    [SerializeField] private Texture2D _photoTexture;

    public void SetSelectedPhotos(List<Texture2D> selectedPhotos)
    {
        _selectedPhotos = selectedPhotos;
    }

    public override void Initialize()
    {
        _toMainScreenButton.onClick.RemoveAllListeners();
        _toMainScreenButton.onClick.AddListener(OnMainScreenPressed);
        _checkDisksButton.onClick.RemoveAllListeners();
        _checkDisksButton.onClick.AddListener(OnCheckDisksPressed);
    }

    public override IEnumerator AnimateShow()
    {
        PreparePhotos();
        _canUpload = true;
        _posterImage.sprite = TextureConverter.ConvertTextureToSprite(GlobalChosesDataContainer.Instance.Poster);
        yield return AnimateFadeIn(_canvasGroup, _fadeDuration);
        bool diskAvaliable = _loader.CheckAvaliableDisks();
        diskAvaliableIcon.gameObject.SetActive(diskAvaliable);
        if (diskAvaliable && _canUpload)
        {
            _canUpload = false;
            _loader.TryUploadPhotosToFlashDisk();
            _canUpload = true;
        }
    }
    public override IEnumerator AnimateHide()
    {
        yield return AnimateFadeOut(_canvasGroup, _fadeDuration);
    }

    public void setupQr(Texture2D tex)
    {
        _qrCodeImage.sprite = TextureConverter.ConvertTextureToSprite(tex);

        _qrCodeImagePreloaderCG.DOFade(0, 1);

        _posterImagePreloaderCG.DOFade(0, 1);

        _posterImage.GetComponent<CanvasGroup>().DOFade(1, 1);

        _photosDownloaded = true; ;
    }
    private async void PreparePhotos()
    {
        int session = GlobalChosesDataContainer.Instance.SessionId;
        _photosDownloaded = false;
        _qrCodeImage.sprite = null;
        _posterImage.sprite = null;
        _posterImage.GetComponent<CanvasGroup>().DOFade(0, 0);
        _qrCodeImagePreloaderCG.DOFade(1, 0);
        _posterImagePreloaderCG.DOFade(1, 0);
        while (!_loader.posterCreationFinished)
        {
            await Task.Delay(100);
            if (session != GlobalChosesDataContainer.Instance.SessionId) return;
        }
        var poster = GlobalChosesDataContainer.Instance.Poster;
        if (poster == null)
        {
            _photosDownloaded = true;
            return;
        }
        _posterImage.sprite = TextureConverter.ConvertTextureToSprite(poster);
        bool uploaded = await _loader.UploadSelectedPhotosToDisk(new[] { poster }, true);
        if (session != GlobalChosesDataContainer.Instance.SessionId) return;
        _photosDownloaded = true;
        if (uploaded) _loader.TryUploadPhotosToFlashDisk();
    }
    private void OnCheckDisksPressed()
    {
        if (!_photosDownloaded)
        {
            return;
        }
        bool diskAvaliable = _loader.CheckAvaliableDisks();
        diskAvaliableIcon.gameObject.SetActive(diskAvaliable);
        if (diskAvaliable && _canUpload)
        {
            _canUpload = false;
            _loader.TryUploadPhotosToFlashDisk();
            _canUpload = true;
        }
    }

    private void OnMainScreenPressed()
    {
        if (!_photosDownloaded)
        {
            return;
        }

        ScreenManager.Instance.ResetSession();

        // ScreenManager.Instance.ShowScreen<ScreensaverScreen>();
    }

}
