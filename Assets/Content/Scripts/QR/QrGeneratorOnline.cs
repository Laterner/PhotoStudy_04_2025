using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Linq;
using System;

public class QrGeneratorOnline : MonoBehaviour
{
    private string API_URL = "https://devdeployment.ru/2025.2.ar/api/v1/ar-link/upload";
    private string AUTH_TOKEN = "AXh3gf4MYS9wcnsCm8HeQ6";

    [System.Serializable]
    public class LinkData
    {
        public string value;
        public string qrUrl;
        public string qr;
    }

    [System.Serializable]
    public class ResponseData
    {
        public LinkData link;
        public int status;
    }

    [System.Serializable]
    public class ApiResponse
    {
        public bool success;
        public ResponseData data;
    }

    public void Init()
    {
        try
        {
            DirectoryInfo d = new DirectoryInfo(Application.streamingAssetsPath);
            var files = d.GetFiles("*.txt");
            if (files == null)
            {
                throw new System.Exception("NO CONFIGS IN STREAMING ASSETS");
            }
            var config = files.First((x) => x.Name.Contains("qronlineconfig"));
            var sr = config.OpenText();
            var yandexConfig = sr.ReadToEnd();
            var splitPathes = yandexConfig.Split(new char[] { '|' });
            API_URL = splitPathes[0];
            AUTH_TOKEN = splitPathes[1];
        }
        catch (Exception e)
        {
            Debug.Log("CHECK CONFIG QR_ONLINE" + e.Message);
        }

    }

    public void UploadPhotoLink(string photoUrl, System.Action<ApiResponse> callback = null)
    {

        StartCoroutine(UploadPhotoLinkCoroutine(photoUrl, callback));
    }

    public IEnumerator UploadPhotoLinkCoroutine(string photoUrl, System.Action<ApiResponse> callback)
    {
        ApiResponse response = null;
        UnityWebRequest request = null;
        try
        {
            var form = new WWWForm();
            form.AddField("link", photoUrl);
            request = UnityWebRequest.Post(API_URL, form);
            request.timeout = 30;
            request.SetRequestHeader("accept", "application/json");
            request.SetRequestHeader("X-Upload-Token", AUTH_TOKEN);
        }
        catch (Exception e) { Debug.LogWarning(e.Message); }
        try
        {
            if (request != null)
            {
                UnityWebRequestAsyncOperation operation = null;
                try { operation = request.SendWebRequest(); }
                catch (Exception e) { Debug.LogWarning(e.Message); }
                if (operation != null) yield return operation;
                try
                {
                    if (operation != null && request.result == UnityWebRequest.Result.Success)
                        response = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                }
                catch (Exception e) { Debug.LogWarning("Invalid QR response: " + e.Message); }
            }
        }
        finally
        {
            request?.Dispose();
            callback?.Invoke(response);
        }
    }
}
