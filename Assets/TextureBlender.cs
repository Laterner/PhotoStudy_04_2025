using UnityEngine;
using UnityEngine.UI;

public class TextureBlender : MonoBehaviour
{
    public Texture2D baseTexture;
    public Texture2D overlayTexture;

    void Start()
    {
        // Создаем UI материал
        Material uiMaterial = new Material(Shader.Find("UI/Default"));

        // Смешиваем текстуры
        Texture2D blendedTexture = BlendTextures(baseTexture, overlayTexture);

        // Применяем к UI Image
        Image image = GetComponent<Image>();
        if (image != null)
        {
            uiMaterial.mainTexture = blendedTexture;
            image.material = uiMaterial;

            // Создаем спрайт из текстуры
            image.sprite = Sprite.Create(blendedTexture,
                new Rect(0, 0, blendedTexture.width, blendedTexture.height),
                new Vector2(0.5f, 0.5f));
        }
    }

    Texture2D BlendTextures(Texture2D bg, Texture2D fg)
    {
        Texture2D result = new Texture2D(bg.width, bg.height);

        for (int x = 0; x < bg.width; x++)
        {
            for (int y = 0; y < bg.height; y++)
            {
                Color bgColor = bg.GetPixel(x, y);
                Color fgColor = fg.GetPixel(x, y);
                Color finalColor = Color.Lerp(bgColor, fgColor, fgColor.a);
                result.SetPixel(x, y, finalColor);
            }
        }

        result.Apply();
        return result;
    }
}