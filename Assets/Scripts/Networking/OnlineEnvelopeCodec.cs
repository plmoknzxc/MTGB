using System;
using System.IO;
using System.IO.Compression;
using System.Text;

/// <summary>Compresses large private projections without altering their JSON or hashes.</summary>
public static class OnlineEnvelopeCodec
{
    public const int CompressionThreshold = 32 * 1024;
    public const int MaxDecodedBytes = 16 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    public static OnlineEnvelope Encode(string type, string payload)
    {
        var envelope = new OnlineEnvelope { type = type, payload = payload ?? string.Empty };
        if (envelope.payload.Length < CompressionThreshold) return envelope;
        byte[] bytes = Utf8.GetBytes(envelope.payload);
        if (bytes.Length > MaxDecodedBytes) throw new InvalidDataException("Network projection exceeds decoded size limit.");
        using (var output = new MemoryStream())
        {
            using (var zip = new GZipStream(output, CompressionLevel.Fastest, true)) zip.Write(bytes, 0, bytes.Length);
            string packed = Convert.ToBase64String(output.GetBuffer(),0,checked((int)output.Length));
            if (packed.Length < envelope.payload.Length)
            {
                envelope.payload = packed;
                envelope.compressed = true;
            }
        }
        return envelope;
    }

    public static void Decode(OnlineEnvelope envelope)
    {
        if (!envelope.compressed) return;
        byte[] packed = Convert.FromBase64String(envelope.payload ?? string.Empty);
        using (var input = new MemoryStream(packed))
        using (var zip = new GZipStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            byte[] buffer = new byte[8192];
            int count;
            while ((count = zip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + count > MaxDecodedBytes) throw new InvalidDataException("Decoded network projection exceeds limit.");
                output.Write(buffer, 0, count);
            }
            envelope.payload = Utf8.GetString(output.GetBuffer(),0,checked((int)output.Length));
            envelope.compressed = false;
        }
    }
}
