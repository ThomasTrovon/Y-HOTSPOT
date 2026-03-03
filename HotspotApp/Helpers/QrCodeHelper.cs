using QRCoder;

namespace HotspotApp.Helpers;

public static class QrCodeHelper
{
    public static byte[] GetWifiQrBytes(string ssid, string passphrase)
    {
        var payload = $"WIFI:T:WPA;S:{Escape(ssid)};P:{Escape(passphrase)};;";
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(20);
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace(":", "\\:");
    }
}
