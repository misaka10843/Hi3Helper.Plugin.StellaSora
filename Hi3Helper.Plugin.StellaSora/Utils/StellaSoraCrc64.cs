using System.IO;

namespace Hi3Helper.Plugin.StellaSora.Utils;

public static class StellaSoraCrc64
{
    private static readonly ulong[] Table = new ulong[256];
    private const ulong Poly = 0xC96C5795D7870F42;

    static StellaSoraCrc64()
    {
        for (int i = 0; i < 256; i++)
        {
            ulong part = (ulong)i;
            for (int j = 0; j < 8; j++)
            {
                if ((part & 1) != 0)
                    part = (part >> 1) ^ Poly;
                else
                    part >>= 1;
            }

            Table[i] = part;
        }
    }

    public static ulong Compute(string filePath)
    {
        if (!File.Exists(filePath)) return 0;

        ulong hash = 0xFFFFFFFFFFFFFFFF;
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072);
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(131072);
        try
        {
            int read;
            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    hash = (hash >> 8) ^ Table[(hash ^ buffer[i]) & 0xFF];
                }
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        return hash ^ 0xFFFFFFFFFFFFFFFF;
    }
}