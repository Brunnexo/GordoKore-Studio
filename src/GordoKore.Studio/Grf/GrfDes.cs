namespace GordoKore.Studio.Grf;

/// <summary>
/// DES de uma rodada da Gravity nos arquivos do GRF com flag 0x02 (misto) ou 0x04 (so o cabecalho);
/// os sprites do data.grf usam. Port do GordoExtractor/extractor/grf/des.py (rAthena des.cpp/grfio.cpp).
/// </summary>
internal static class GrfDes
{
    private static readonly byte[] Mask = { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };

    private static readonly byte[] IpTable =
    {
        58, 50, 42, 34, 26, 18, 10, 2, 60, 52, 44, 36, 28, 20, 12, 4,
        62, 54, 46, 38, 30, 22, 14, 6, 64, 56, 48, 40, 32, 24, 16, 8,
        57, 49, 41, 33, 25, 17, 9, 1, 59, 51, 43, 35, 27, 19, 11, 3,
        61, 53, 45, 37, 29, 21, 13, 5, 63, 55, 47, 39, 31, 23, 15, 7,
    };

    private static readonly byte[] FpTable =
    {
        40, 8, 48, 16, 56, 24, 64, 32, 39, 7, 47, 15, 55, 23, 63, 31,
        38, 6, 46, 14, 54, 22, 62, 30, 37, 5, 45, 13, 53, 21, 61, 29,
        36, 4, 44, 12, 52, 20, 60, 28, 35, 3, 43, 11, 51, 19, 59, 27,
        34, 2, 42, 10, 50, 18, 58, 26, 33, 1, 41, 9, 49, 17, 57, 25,
    };

    private static readonly byte[] TpTable =
    {
        16, 7, 20, 21, 29, 12, 28, 17, 1, 15, 23, 26, 5, 18, 31, 10,
        2, 8, 24, 14, 32, 27, 3, 9, 19, 13, 30, 6, 22, 11, 4, 25,
    };

    private static readonly byte[][] STable =
    {
        new byte[]
        {
            0xef, 0x03, 0x41, 0xfd, 0xd8, 0x74, 0x1e, 0x47, 0x26, 0xef, 0xfb, 0x22, 0xb3, 0xd8, 0x84, 0x1e,
            0x39, 0xac, 0xa7, 0x60, 0x62, 0xc1, 0xcd, 0xba, 0x5c, 0x96, 0x90, 0x59, 0x05, 0x3b, 0x7a, 0x85,
            0x40, 0xfd, 0x1e, 0xc8, 0xe7, 0x8a, 0x8b, 0x21, 0xda, 0x43, 0x64, 0x9f, 0x2d, 0x14, 0xb1, 0x72,
            0xf5, 0x5b, 0xc8, 0xb6, 0x9c, 0x37, 0x76, 0xec, 0x39, 0xa0, 0xa3, 0x05, 0x52, 0x6e, 0x0f, 0xd9,
        },
        new byte[]
        {
            0xa7, 0xdd, 0x0d, 0x78, 0x9e, 0x0b, 0xe3, 0x95, 0x60, 0x36, 0x36, 0x4f, 0xf9, 0x60, 0x5a, 0xa3,
            0x11, 0x24, 0xd2, 0x87, 0xc8, 0x52, 0x75, 0xec, 0xbb, 0xc1, 0x4c, 0xba, 0x24, 0xfe, 0x8f, 0x19,
            0xda, 0x13, 0x66, 0xaf, 0x49, 0xd0, 0x90, 0x06, 0x8c, 0x6a, 0xfb, 0x91, 0x37, 0x8d, 0x0d, 0x78,
            0xbf, 0x49, 0x11, 0xf4, 0x23, 0xe5, 0xce, 0x3b, 0x55, 0xbc, 0xa2, 0x57, 0xe8, 0x22, 0x74, 0xce,
        },
        new byte[]
        {
            0x2c, 0xea, 0xc1, 0xbf, 0x4a, 0x24, 0x1f, 0xc2, 0x79, 0x47, 0xa2, 0x7c, 0xb6, 0xd9, 0x68, 0x15,
            0x80, 0x56, 0x5d, 0x01, 0x33, 0xfd, 0xf4, 0xae, 0xde, 0x30, 0x07, 0x9b, 0xe5, 0x83, 0x9b, 0x68,
            0x49, 0xb4, 0x2e, 0x83, 0x1f, 0xc2, 0xb5, 0x7c, 0xa2, 0x19, 0xd8, 0xe5, 0x7c, 0x2f, 0x83, 0xda,
            0xf7, 0x6b, 0x90, 0xfe, 0xc4, 0x01, 0x5a, 0x97, 0x61, 0xa6, 0x3d, 0x40, 0x0b, 0x58, 0xe6, 0x3d,
        },
        new byte[]
        {
            0x4d, 0xd1, 0xb2, 0x0f, 0x28, 0xbd, 0xe4, 0x78, 0xf6, 0x4a, 0x0f, 0x93, 0x8b, 0x17, 0xd1, 0xa4,
            0x3a, 0xec, 0xc9, 0x35, 0x93, 0x56, 0x7e, 0xcb, 0x55, 0x20, 0xa0, 0xfe, 0x6c, 0x89, 0x17, 0x62,
            0x17, 0x62, 0x4b, 0xb1, 0xb4, 0xde, 0xd1, 0x87, 0xc9, 0x14, 0x3c, 0x4a, 0x7e, 0xa8, 0xe2, 0x7d,
            0xa0, 0x9f, 0xf6, 0x5c, 0x6a, 0x09, 0x8d, 0xf0, 0x0f, 0xe3, 0x53, 0x25, 0x95, 0x36, 0x28, 0xcb,
        },
    };

    private static byte Substitute(byte b) => b switch
    {
        0x00 => 0x2B, 0x2B => 0x00, 0x6C => 0x80, 0x80 => 0x6C, 0x01 => 0x68, 0x68 => 0x01, 0x48 => 0x77,
        0x77 => 0x48, 0x60 => 0xFF, 0xFF => 0x60, 0xB9 => 0xC0, 0xC0 => 0xB9, 0xFE => 0xEB, 0xEB => 0xFE,
        _ => b
    };

    private static void Permute(Span<byte> block, byte[] table)
    {
        Span<byte> output = stackalloc byte[8];
        output.Clear();
        for (int i = 0; i < 64; i++)
        {
            int j = table[i] - 1;
            if ((block[(j >> 3) & 7] & Mask[j & 7]) != 0)
                output[(i >> 3) & 7] |= Mask[i & 7];
        }
        output.CopyTo(block);
    }

    private static void Round(Span<byte> b)
    {
        Span<byte> e = stackalloc byte[8];
        e[0] = (byte)(((b[7] << 5) | (b[4] >> 3)) & 0x3F);
        e[1] = (byte)(((b[4] << 1) | (b[5] >> 7)) & 0x3F);
        e[2] = (byte)(((b[4] << 5) | (b[5] >> 3)) & 0x3F);
        e[3] = (byte)(((b[5] << 1) | (b[6] >> 7)) & 0x3F);
        e[4] = (byte)(((b[5] << 5) | (b[6] >> 3)) & 0x3F);
        e[5] = (byte)(((b[6] << 1) | (b[7] >> 7)) & 0x3F);
        e[6] = (byte)(((b[6] << 5) | (b[7] >> 3)) & 0x3F);
        e[7] = (byte)(((b[7] << 1) | (b[4] >> 7)) & 0x3F);

        Span<byte> s = stackalloc byte[8];
        s.Clear();
        for (int i = 0; i < 4; i++)
            s[i] = (byte)((STable[i][e[i * 2]] & 0xF0) | (STable[i][e[i * 2 + 1]] & 0x0F));

        Span<byte> t = stackalloc byte[8];
        t.Clear();
        for (int i = 0; i < 32; i++)
        {
            int j = TpTable[i] - 1;
            if ((s[j >> 3] & Mask[j & 7]) != 0)
                t[(i >> 3) + 4] |= Mask[i & 7];
        }
        b[0] ^= t[4];
        b[1] ^= t[5];
        b[2] ^= t[6];
        b[3] ^= t[7];
    }

    private static void DecryptBlock(Span<byte> block)
    {
        Permute(block, IpTable);
        Round(block);
        Permute(block, FpTable);
    }

    private static void Shuffle(Span<byte> b)
    {
        Span<byte> s = stackalloc byte[8];
        b.CopyTo(s);
        b[0] = s[3];
        b[1] = s[4];
        b[2] = s[6];
        b[3] = s[0];
        b[4] = s[1];
        b[5] = s[2];
        b[6] = s[5];
        b[7] = Substitute(s[7]);
    }

    private static int Cycle(int compressedLength)
    {
        int digits = 1;
        for (long i = 10; i <= compressedLength; i *= 10)
            digits++;
        return digits < 3 ? 1 : digits < 5 ? digits + 1 : digits < 7 ? digits + 9 : digits + 15;
    }

    /// <summary>Flag 0x02: 20 primeiros blocos e um a cada ciclo cifrados; os outros embaralhados de 7 em 7.</summary>
    public static void DecodeFull(byte[] data, int compressedLength)
    {
        int blocks = data.Length / 8, cycle = Cycle(compressedLength), j = -1;
        for (int i = 0; i < blocks; i++)
        {
            var block = data.AsSpan(i * 8, 8);
            if (i < 20 || i % cycle == 0)
            {
                DecryptBlock(block);
                continue;
            }
            j++;
            if (j % 7 == 0 && j != 0)
                Shuffle(block);
        }
    }

    /// <summary>Flag 0x04: so os 20 primeiros blocos.</summary>
    public static void DecodeHeader(byte[] data)
    {
        for (int i = 0; i < Math.Min(20, data.Length / 8); i++)
            DecryptBlock(data.AsSpan(i * 8, 8));
    }
}
