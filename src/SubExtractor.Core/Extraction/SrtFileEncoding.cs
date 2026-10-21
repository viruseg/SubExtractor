using System.Text;

namespace SubExtractor.Core.Extraction;

internal static class SrtFileEncoding
{
    public static (Encoding Encoding, int PreambleLength) Detect(byte[] bytes) =>
        bytes switch
        {
            [0xEF, 0xBB, 0xBF, ..] => (Encoding.UTF8, 3),
            [0xFF, 0xFE, ..] => (Encoding.Unicode, 2),
            [0xFE, 0xFF, ..] => (Encoding.BigEndianUnicode, 2),
            _ => (Encoding.UTF8, 0),
        };
}
