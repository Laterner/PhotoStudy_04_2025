using UnityEngine;
using ZXing;

public class QRCodeDrawing
{
    // The caller owns the returned texture.
    public Texture2D DrowQRCode(string formatStr)
    {
        var options = new ZXing.QrCode.QrCodeEncodingOptions
        {
            CharacterSet = "UTF-8", Width = 256, Height = 256, Margin = 1
        };
        var writer = new BarcodeWriter { Format = BarcodeFormat.QR_CODE, Options = options };
        var colors = writer.Write(formatStr);
        var texture = new Texture2D(256, 256);
        try
        {
            texture.SetPixels32(colors);
            texture.Apply();
            return texture;
        }
        catch { TextureConverter.DestroyTexture(texture); throw; }
    }
}
