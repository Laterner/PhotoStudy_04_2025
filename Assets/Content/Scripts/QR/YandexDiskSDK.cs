using System;
using System.Collections;
using System.IO;
using System.Linq;

using System.Net.Http;
using System.Threading.Tasks;


using UnityEngine;

namespace Project
{
    public class YandexDiskSDK : MonoBehaviour
    {
        private string token;
        private string sourcePath = "";
        private string pathDisk = "";
        private string pathDiskName;// = "Photo-Posters";
        private string urlToDownloadedFile = "";


        private string GetPathDisk(string folder, string date, string nameFile) => folder + "/" + date + "/" + nameFile;

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
                var config = files.First((x) => x.Name.Contains("yandex"));
                var sr = config.OpenText();
                var yandexConfig = sr.ReadToEnd();
                var splitPathes = yandexConfig.Split(new char[] { '|' });
                token = splitPathes[0];
                pathDiskName = splitPathes[1];
            }
            catch (Exception e)
            {
                Debug.Log("CHECK CONFIG YA_DISK " + e.Message);
            }

        }

        private static readonly HttpClient Client = new HttpClient();

        private static bool IsCurrent(int session) => GlobalChosesDataContainer.Instance.SessionId == session;

        public async Task<string> SaveFilesToDisk(string sourcePath, string folderName, string[] nameFiles)
        {
            int session = GlobalChosesDataContainer.Instance.SessionId;
            try
            {
                if (!GlobalChosesDataContainer.Instance.YDFolderCreated)
                {
                    await Send(HttpMethod.Put, "https://cloud-api.yandex.net/v1/disk/resources?path=" + pathDiskName + "/" + folderName, session);
                    if (!IsCurrent(session)) return null;
                    await Send(HttpMethod.Put, "https://cloud-api.yandex.net/v1/disk/resources/publish?path=" + pathDiskName + "/" + folderName + "&overwrite=true", session);
                    if (!IsCurrent(session)) return null;
                    GlobalChosesDataContainer.Instance.YDFolderCreated = true;
                }
                foreach (string name in nameFiles)
                {
                    if (!IsCurrent(session)) return null;
                    // Preserve this checkout's local Yandex.Disk synchronization workflow.
                    string destination = $"C:/Pattern/YDFiles/{folderName}";
                    Directory.CreateDirectory(destination);
                    File.Copy(Path.Combine(sourcePath, name), Path.Combine(destination, name), true);
                }
                string body = await Send(HttpMethod.Get, "https://cloud-api.yandex.net/v1/disk/resources?path=" + pathDiskName + "/" + folderName, session);
                if (!IsCurrent(session)) return null;
                return ExtractUrl(body, "public_url");
            }
            catch (Exception e)
            {
                if (IsCurrent(session)) Debug.LogWarning("Yandex.Disk: " + e.Message);
                return null;
            }
        }

        private async Task<string> Send(HttpMethod method, string url, int session)
        {
            using (var request = new HttpRequestMessage(method, url))
            {
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Authorization", $"OAuth {token}");
                using (var response = await Client.SendAsync(request))
                {
                    if (!IsCurrent(session)) return null;
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync();
                    return IsCurrent(session) ? body : null;
                }
            }
        }

        private static string ExtractUrl(string body, string key)
        {
            if (string.IsNullOrEmpty(body)) return null;
            int keyIndex = body.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            int start = body.IndexOf("https", keyIndex >= 0 ? keyIndex : 0, StringComparison.Ordinal);
            if (start < 0) return null;
            int end = body.IndexOf('"', start);
            return end < 0 ? null : body.Substring(start, end - start);
        }

    }
}
