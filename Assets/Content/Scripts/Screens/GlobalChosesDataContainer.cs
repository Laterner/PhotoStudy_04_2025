using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalChosesDataContainer : MonoBehaviour
{
    public static GlobalChosesDataContainer Instance { get; private set; }
    public ShootingModeSelectionScreen.ShootingMode ShootingMode { get; set; }
    public LightingSchemeData LightingMode { get; set; }
    public List<Texture2D> Photos { get; } = new List<Texture2D>();
    public List<Texture2D> SelectedPhotos { get; set; } = new List<Texture2D>();
    public Texture2D QrCode { get; private set; }
    public Texture2D Poster { get; private set; }
    public int SessionId { get; private set; }
    public string CurrentFolderName = "";
    public string Name { get; set; } = "";
    public string Surname { get; set; } = "";
    public bool YDFolderCreated = false;
    public bool isDoubleBuild;

    public int SelectedCategory { get; set; }
    public void Init()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public void ClearPhotos()
    {
        foreach (var photo in Photos) TextureConverter.DestroyTexture(photo);
        Photos.Clear();
        SelectedPhotos.Clear();
    }
    public void SetPoster(Texture2D texture)
    {
        if (Poster == texture) return;
        TextureConverter.DestroyTexture(Poster);
        Poster = texture;
    }
    public void SetQrCode(Texture2D texture)
    {
        if (QrCode == texture) return;
        TextureConverter.DestroyTexture(QrCode);
        QrCode = texture;
    }
    public void Clean()
    {
        SessionId++;
        ClearPhotos();
        SetPoster(null);
        SetQrCode(null);
        Name = "";
        Surname = "";
        CurrentFolderName = "";
        YDFolderCreated = false;
    }
}
