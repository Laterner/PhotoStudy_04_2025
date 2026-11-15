using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Project;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;


public class Loader : MonoBehaviour
{
    public bool IsPortrait;
    [SerializeField] private string FilesPath;
    [SerializeField] private string OutFilesPath;
    public static Action<DateTime> filesLoaded;
    // [SerializeField] private PhotosContainer container;
    [SerializeField] private GlobalChosesDataContainer container;
    [SerializeField] private YandexDiskSDK yandex;
    [SerializeField] private RemovableMediaManager flashDiskUploadManager;
    public delegate void VoidDelegate(bool canCount);
    public static VoidDelegate CanCount;
    public VideoPlayer vp;
    public delegate void RepeatDelegate();
    // public static RepeatDelegate RepeatDrawQr;
    private bool canCount;
    [SerializeField] private float checkDelay, timer;
    public delegate Task UploadDelegate();
    // public static UploadDelegate UploadPhotos;
    private string downloadResult;
    private List<string> downloadedFiles;
    private string photoFormat = "jpg";
    [SerializeField] private string folderName = "";
    public bool posterCreationFinished;

    public int sensitivity = 20;


    [SerializeField] private Texture2D overlayTexture;
    [SerializeField] private Material blendMaterial;


    public async void Init()
    {
        DirectoryInfo d = new DirectoryInfo(Application.streamingAssetsPath);
        var files = d.GetFiles("*.txt");
        if (files == null)
        {
            throw new System.Exception("NO CONFIGS IN STREAMING ASSETS");
        }
        var config = files.First((x) => x.Name.Contains("config"));
        var sr = config.OpenText();
        var pathes = sr.ReadToEnd();


        var format = Array.Find(files, (x) => x.Name.Contains("format"));
        if (format != null)
        {
            var formatFile = files.First((x) => x.Name.Contains("format"));
            var formatSr = config.OpenText();
            photoFormat = sr.ReadToEnd();
        }

        var splitPathes = pathes.Split(new char[] { '|' });
        FilesPath = splitPathes[0];
        OutFilesPath = splitPathes[1];

        try
        {
            sensitivity = int.Parse(splitPathes[2]);
        }
        catch
        {
            sensitivity = 20;
        }

        //Debug.Log(FilesPath + " | " + OutFilesPath);
        timer = 0;
        canCount = true;

        downloadedFiles = new List<string>();
        posterCreationFinished = false;

        StartMonitoring();
    }

    ////////////////////////////////////////////////////////////////////NEW LOADER LOGIC///////////////////////////////////////////////////////////////////////////
    public event Action<Texture2D> OnNewPhotoLoaded;
    public event Action OnAllPhotosLoaded;

    [SerializeField] private int _expectedPhotoCount = 12;
    [SerializeField] private int _checkIntervalMs = 500;

    private HashSet<string> _processedFiles = new HashSet<string>();
    private CancellationTokenSource _monitorCancellation;
    private readonly HashSet<string> _failedFiles = new HashSet<string>();

    public async void StartMonitoring()
    {
        StopMonitoring();
        var cancellation = new CancellationTokenSource();
        _monitorCancellation = cancellation;
        var token = cancellation.Token;
        _processedFiles.Clear();
        _failedFiles.Clear();
        try
        {
            while (_processedFiles.Count < _expectedPhotoCount)
            {
                await CheckForNewPhotos(token);
                token.ThrowIfCancellationRequested();
                if (_processedFiles.Count >= _expectedPhotoCount) break;
                await Task.Delay(_checkIntervalMs, token);
                token.ThrowIfCancellationRequested();
            }
            OnAllPhotosLoaded?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            if (_monitorCancellation == cancellation) _monitorCancellation = null;
            cancellation.Dispose();
        }
    }

    public void StopMonitoring() { _monitorCancellation?.Cancel(); }
    private void OnDestroy() { StopMonitoring(); }
    public string GetFilePath() => FilesPath;
    public string GetOutFilePath() => OutFilesPath;

    private async Task CheckForNewPhotos(CancellationToken token)
    {
        var files = new DirectoryInfo(FilesPath).GetFiles($"*.{photoFormat}");
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            if (_processedFiles.Contains(file.Name)) continue;
            var texture = await LoadTexture(file.FullName, token);
            if (token.IsCancellationRequested)
            {
                TextureConverter.DestroyTexture(texture);
                token.ThrowIfCancellationRequested();
            }
            if (texture == null) continue;
            _processedFiles.Add(file.Name);
            // The review screen transfers this texture to the session owner.
            if (OnNewPhotoLoaded == null) TextureConverter.DestroyTexture(texture);
            else OnNewPhotoLoaded.Invoke(texture);
            if (_processedFiles.Count >= _expectedPhotoCount) break;
        }
    }

    private async Task<Texture2D> LoadTexture(string filePath, CancellationToken token)
    {
        Texture2D texture = null;
        try
        {
            byte[] fileData = await File.ReadAllBytesAsync(filePath, token);
            token.ThrowIfCancellationRequested();
            texture = new Texture2D(2, 2);
            if (!texture.LoadImage(fileData))
                throw new IOException("Image is not complete yet.");
            if (!IsPortrait) return texture;
            var rotated = TextureConverter.RotateTexture(texture);
            TextureConverter.DestroyTexture(texture);
            return rotated;
        }
        catch (OperationCanceledException)
        {
            TextureConverter.DestroyTexture(texture);
            throw;
        }
        catch (Exception e)
        {
            TextureConverter.DestroyTexture(texture);
            if (_failedFiles.Add(filePath))
                Debug.LogWarning($"Failed to load {filePath}: {e.Message}. Will retry.");
            return null;
        }
    }
    private async Task<Texture2D> BlendTexturesAsync(Texture2D baseTexture,
                                             IProgress<float> progress = null,
                                             CancellationToken cancellationToken = default)
    {
        int session = container.SessionId;
        Texture2D resizedOverlay = null;
        Texture2D result = null;

        try
        {
            // Проверяем размеры текстур
            if (baseTexture.width != overlayTexture.width || baseTexture.height != overlayTexture.height)
            {
                resizedOverlay = ResizeTexture(overlayTexture, baseTexture.width, baseTexture.height);
            }
            else
            {
                resizedOverlay = overlayTexture;
            }

            // Convert the base mip to Color32; raw RGB24 bytes are not Color32 pixels.
            var baseData = baseTexture.GetPixels32(0);
            var overlayData = resizedOverlay.GetPixels32(0);
            int pixelCount = checked(baseTexture.width * baseTexture.height);
            if (baseData.Length != pixelCount || overlayData.Length != pixelCount)
                throw new InvalidOperationException("Photo and overlay pixel counts must match their dimensions.");
            var resultData = new Color32[pixelCount];

            int totalPixels = pixelCount;
            int chunkSize = Mathf.Max(1024, totalPixels / 100);
            int progressStep = Mathf.Max(1, totalPixels / 100);
            int nextProgressReport = 0;

            for (int i = 0; i < totalPixels; i += chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int endIndex = Mathf.Min(i + chunkSize, totalPixels);

                ProcessChunkFast(baseData, overlayData, resultData, i, endIndex);

                if (i >= nextProgressReport)
                {
                    progress?.Report((float)i / totalPixels);
                    nextProgressReport += progressStep;
                }

                if ((i / chunkSize) % 10 == 0)
                {
                    await Task.Yield();
                    if (session != container.SessionId) throw new OperationCanceledException();
                }
            }

            result = new Texture2D(baseTexture.width, baseTexture.height,
                                   TextureFormat.RGBA32, false);
            result.SetPixelData(resultData, 0);
            result.Apply(false);

            progress?.Report(1f);
            return result;
        }
        catch (Exception)
        {
            if (result != null)
            {
                TextureConverter.DestroyTexture(result);
            }
            throw;
        }
        finally
        {
            if (resizedOverlay != null && resizedOverlay != overlayTexture)
            {
                TextureConverter.DestroyTexture(resizedOverlay);
            }
        }
    }

    // Простая версия с обычными массивами
    private void ProcessChunkFast(Color32[] baseData, Color32[] overlayData,
                                  Color32[] resultData, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            var bgColor = baseData[i];
            var fgColor = overlayData[i];

            float alpha = fgColor.a / 255f;
            float invAlpha = 1f - alpha;

            resultData[i] = new Color32(
                (byte)(bgColor.r * invAlpha + fgColor.r * alpha),
                (byte)(bgColor.g * invAlpha + fgColor.g * alpha),
                (byte)(bgColor.b * invAlpha + fgColor.b * alpha),
                (byte)(bgColor.a * invAlpha + fgColor.a * alpha)
            );
        }
    }

    // Оптимизированный метод изменения размера текстуры с правильным управлением памятью
    private Texture2D ResizeTexture(Texture2D sourceTexture, int newWidth, int newHeight)
    {
        // Проверяем, не нужно ли вообще менять размер
        if (sourceTexture.width == newWidth && sourceTexture.height == newHeight)
        {
            return sourceTexture;
        }

        var previous = RenderTexture.active;
        RenderTexture rt = null;
        Texture2D resizedTexture = null;

        try
        {
            // Создаём RenderTexture с использованием GetTemporary для пулинга
            rt = RenderTexture.GetTemporary(newWidth, newHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;

            // Копируем исходную текстуру в RenderTexture с новым размером
            Graphics.Blit(sourceTexture, rt);

            // Создаём новую текстуру нужного размера
            resizedTexture = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resizedTexture.Apply(false); // false - не обновлять mipmaps

            return resizedTexture;
        }
        catch { TextureConverter.DestroyTexture(resizedTexture); throw; }
        finally
        {
            // Правильная очистка RenderTexture
            RenderTexture.active = previous;
            if (rt != null)
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }


    public async Task<bool> UploadSelectedPhotosToDisk(Texture2D[] textures, bool isPoster = false)
    {
        int session = container.SessionId;
        try
        {
            if (textures == null || textures.Length == 0) throw new IOException("No photos to upload.");
            if (string.IsNullOrEmpty(container.CurrentFolderName))
                container.CurrentFolderName = DateTime.Now.ToSafeString().Replace(" ", "").Replace(":", "").Replace(".", "").Replace("_", "");
            folderName = container.CurrentFolderName;
            string uploadFolder = folderName;
            var names = new string[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                if (session != container.SessionId) return false;
                names[i] = isPoster ? $"Poster.{photoFormat}" : $"{i}.{photoFormat}";
                Texture2D temporary = null;
                try
                {
                    if (!isPoster)
                    {
                        temporary = await BlendTexturesAsync(textures[i]);
                        if (session != container.SessionId) return false;
                    }
                    bool saved = await SaveScreenshotToFile(isPoster ? textures[i] : temporary, uploadFolder, names[i]);
                    if (session != container.SessionId) return false;
                    if (!saved) throw new IOException("Could not save photo.");
                }
                finally { TextureConverter.DestroyTexture(temporary); }
            }
            string url = await yandex.SaveFilesToDisk(Path.Combine(OutFilesPath, uploadFolder), uploadFolder, names);
            if (session != container.SessionId) return false;
            if (string.IsNullOrEmpty(url)) throw new IOException("Could not upload photos.");
            var completion = new TaskCompletionSource<bool>();
            StartCoroutine(CreateQrCode(url, isPoster, session, completion));
            bool success = await completion.Task;
            if (session != container.SessionId) return false;
            if (!success) throw new IOException("Could not create QR code.");
            return true;
        }
        catch (Exception e)
        {
            if (session == container.SessionId)
            {
                Debug.LogWarning(e.Message);
                ScreenManager.Instance.EnableError();
            }
            return false;
        }
    }

    private IEnumerator CreateQrCode(string url, bool isPoster, int session, TaskCompletionSource<bool> completion)
    {
        Texture2D texture = null;
        try
        {
            QrGeneratorOnline.ApiResponse response = null;
            var generator = GetComponent<QrGeneratorOnline>();
            if (generator != null)
            {
                yield return generator.UploadPhotoLinkCoroutine(url, result => response = result);
                if (session != container.SessionId) yield break;
            }
            string qrUrl = response != null && response.success ? response.data?.link?.qr : null;
            UnityWebRequest request = null;
            try
            {
                if (!string.IsNullOrEmpty(qrUrl)) request = UnityWebRequestTexture.GetTexture(qrUrl);
            }
            catch (Exception e) { Debug.LogWarning(e.Message); }
            if (request != null)
            {
                using (request)
                {
                    request.timeout = 30;
                    UnityWebRequestAsyncOperation operation = null;
                    try { operation = request.SendWebRequest(); }
                    catch (Exception e) { Debug.LogWarning(e.Message); }
                    if (operation != null) yield return operation;
                    if (operation != null && request.result == UnityWebRequest.Result.Success)
                    {
                        try { texture = DownloadHandlerTexture.GetContent(request); }
                        catch (Exception e) { Debug.LogWarning(e.Message); }
                    }
                    if (session != container.SessionId) yield break;
                }
            }
            try
            {
                if (texture == null) texture = new QRCodeDrawing().DrowQRCode(url);
                if (texture != null)
                {
                    container.SetQrCode(texture);
                    texture = null; // Ownership transferred.
                    if (isPoster) ScreenManager.Instance.GetScreen<QrCodeCoverScreen>().setupQr(container.QrCode);
                    else ScreenManager.Instance.GetScreen<QrCodeScreen>().setupQr(container.QrCode);
                    completion.TrySetResult(true);
                }
            }
            catch (Exception e) { Debug.LogWarning(e.Message); }
        }
        finally
        {
            TextureConverter.DestroyTexture(texture);
            completion.TrySetResult(false);
        }
    }

    private async Task<bool> SaveScreenshotToFile(Texture2D texture, string uploadFolder, string name)
    {
        try
        {
            byte[] bytes = texture.EncodeToJPG();
            string path = Path.Combine(OutFilesPath, uploadFolder);
            Directory.CreateDirectory(path);
            await File.WriteAllBytesAsync(Path.Combine(path, name), bytes);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning(e.Message);
            return false;
        }
    }
    public void CleanFolder()
    {
        var directory = new DirectoryInfo(FilesPath);
        // var files = directory.GetFiles($"*.{photoFormat}");
        var files = directory.GetFiles($"*.*");
        foreach (var file in files)
        {
            string FilePath = Path.Combine(FilesPath, file.Name);
            try { File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning($"Cannot delete {file.Name}: {e.Message}"); }
        }
    }

    public void CaptureAreaAndSave(RectTransform uiElement, string folderPath, string fileName,
                            int targetWidth = 1080, int targetHeight = 1920)
    {
        posterCreationFinished = false;
        StartCoroutine(CaptureUIAreaCoroutine(uiElement, folderPath, fileName, targetWidth, targetHeight));
    }

    private IEnumerator CaptureUIAreaCoroutine(RectTransform uiElement, string folderPath, string fileName,
                                             int targetWidth, int targetHeight)
    {
        int session = container.SessionId;
        try
        {
            yield return new WaitForEndOfFrame();
            if (session != container.SessionId) yield break;
            try { container.SetPoster(CaptureUIArea(uiElement, targetWidth, targetHeight)); }
            catch (Exception e)
            {
                container.SetPoster(null);
                Debug.LogException(e);
            }
        }
        finally
        {
            if (session == container.SessionId) posterCreationFinished = true;
        }
    }

    private Texture2D CaptureUIArea(RectTransform uiElement, int targetWidth, int targetHeight)
    {
        Canvas canvas = uiElement.GetComponentInParent<Canvas>();
        Vector3[] worldCorners = new Vector3[4];
        uiElement.GetWorldCorners(worldCorners);
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera ?? Camera.main;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[2]);
        Rect rect = new Rect(Mathf.Max(0, min.x), Mathf.Max(0, min.y),
            Mathf.Min(Screen.width, max.x - min.x), Mathf.Min(Screen.height, max.y - min.y));
        if (rect.width <= 10 || rect.height <= 10) return null;
        Texture2D screenTex = null;
        Texture2D croppedTex = null;
        try
        {
            screenTex = ScreenCapture.CaptureScreenshotAsTexture();
            croppedTex = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            croppedTex.SetPixels(screenTex.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
            croppedTex.Apply();
            if (targetWidth != croppedTex.width || targetHeight != croppedTex.height)
                return ScaleTexture(croppedTex, croppedTex.width * 2, croppedTex.height * 2);
            var result = croppedTex;
            croppedTex = null;
            return result;
        }
        finally
        {
            TextureConverter.DestroyTexture(screenTex);
            TextureConverter.DestroyTexture(croppedTex);
        }
    }

    private Texture2D ScaleTexture(Texture2D source, int newWidth, int newHeight)
    {
        source.filterMode = FilterMode.Bilinear;
        var previous = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(newWidth, newHeight);
        Texture2D scaledTex = null;
        try
        {
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            scaledTex = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            scaledTex.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            scaledTex.Apply();
            return scaledTex;
        }
        catch { TextureConverter.DestroyTexture(scaledTex); throw; }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
    public void TryUploadPhotosToFlashDisk()
    {
        flashDiskUploadManager.CopyFolderToRemovableDrive($"{OutFilesPath}/{folderName}");
    }

    public bool CheckAvaliableDisks()
    {
        return !string.IsNullOrEmpty(flashDiskUploadManager.GetRemovableDrivePath());
    }


}
