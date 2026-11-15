using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

public static class TextureConverter
{
    private static readonly Dictionary<Texture2D, Sprite> Sprites = new Dictionary<Texture2D, Sprite>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        foreach (var sprite in Sprites.Values)
            if (sprite != null) Object.Destroy(sprite);
        Sprites.Clear();
    }
    public static Sprite ConvertTextureToSprite(Texture2D texture, Vector2? pivot = null,
        float pixelsPerUnit = 100f, SpriteMeshType meshType = SpriteMeshType.FullRect)
    {
        if (texture == null) return null;
        if (Sprites.TryGetValue(texture, out var sprite) && sprite != null) return sprite;
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            pivot ?? new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, meshType);
        Sprites[texture] = sprite;
        return sprite;
    }
    public static void DestroyTexture(Texture2D texture)
    {
        if (ReferenceEquals(texture, null)) return;
        if (Sprites.TryGetValue(texture, out var sprite))
        {
            Sprites.Remove(texture);
            if (sprite != null) Object.Destroy(sprite);
        }
        if (texture != null) Object.Destroy(texture);
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Rgb24 { public byte R, G, B; }
    private static void RotatePixels<T>(NativeArray<T> source, NativeArray<T> destination,
        int width, int height) where T : struct
    {
        int i = 0;
        for (int x = 0; x < width; x++)
            for (int y = height - 1; y >= 0; y--)
                destination[i++] = source[x + y * width];
    }
    public static Texture2D RotateTexture(Texture2D source)
    {
        bool rawFormat = source.format == TextureFormat.RGB24 || source.format == TextureFormat.RGBA32 ||
            source.format == TextureFormat.ARGB32 || source.format == TextureFormat.BGRA32;
        var result = new Texture2D(source.height, source.width,
            rawFormat ? source.format : TextureFormat.RGBA32, true);
        try
        {
            switch (source.format)
            {
                case TextureFormat.RGB24:
                    RotatePixels(source.GetPixelData<Rgb24>(0), result.GetPixelData<Rgb24>(0), source.width, source.height);
                    break;
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                    RotatePixels(source.GetPixelData<uint>(0), result.GetPixelData<uint>(0), source.width, source.height);
                    break;
                default:
                    var pixels = source.GetPixels32();
                    var rotated = new Color32[pixels.Length];
                    int i = 0;
                    for (int x = 0; x < source.width; x++)
                        for (int y = source.height - 1; y >= 0; y--)
                            rotated[i++] = pixels[x + y * source.width];
                    result.SetPixels32(rotated);
                    break;
            }
            result.Apply(true);
            return result;
        }
        catch { DestroyTexture(result); throw; }
    }
}
