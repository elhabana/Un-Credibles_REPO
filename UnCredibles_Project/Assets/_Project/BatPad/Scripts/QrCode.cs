using System;
using System.Collections.Generic;
using System.Text;

namespace UnCredibles.BatPad
{
    // Minimal QR Code encoder: byte mode, error correction level M, versions 1-10 (up to ~200 bytes).
    // Ported from Project Nayuki's QR Code generator (MIT license, https://www.nayuki.io/page/qr-code-generator-library).
    // Plain C# with no Unity types so it can be tested on its own.
    public static class QrCode
    {
        private const int MaxVersion = 10;
        private const int EccFormatBits = 0; // level M

        // Indexed by version (index 0 unused), error correction level M.
        private static readonly int[] EccCodewordsPerBlock = { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26 };
        private static readonly int[] EccBlockCount = { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5 };

        private const int PenaltyN1 = 3, PenaltyN2 = 3, PenaltyN3 = 40, PenaltyN4 = 10;

        // Returns the modules as [y, x], true = dark. No quiet zone included.
        public static bool[,] Encode(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);

            int version = 1;
            while (4 + CharCountBits(version) + data.Length * 8 > DataCodewords(version) * 8)
            {
                if (++version > MaxVersion) throw new ArgumentException("Text is too long for this QR encoder.", nameof(text));
            }

            int capacityBits = DataCodewords(version) * 8;
            var bits = new List<bool>(capacityBits);
            AppendBits(bits, 0x4, 4); // byte mode
            AppendBits(bits, data.Length, CharCountBits(version));
            foreach (byte b in data) AppendBits(bits, b, 8);
            AppendBits(bits, 0, Math.Min(4, capacityBits - bits.Count));
            AppendBits(bits, 0, (8 - bits.Count % 8) % 8);
            for (int pad = 0xEC; bits.Count < capacityBits; pad ^= 0xEC ^ 0x11) AppendBits(bits, pad, 8);

            var codewords = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++)
                if (bits[i]) codewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));

            var symbol = new Symbol(version);
            symbol.DrawFunctionPatterns();
            symbol.DrawCodewords(AddEccAndInterleave(codewords, version));
            symbol.ApplyBestMask();
            return symbol.Modules;
        }

        private static int CharCountBits(int version) => version <= 9 ? 8 : 16;

        private static int RawDataModules(int version)
        {
            int result = (16 * version + 128) * version + 64;
            if (version >= 2)
            {
                int alignCount = version / 7 + 2;
                result -= (25 * alignCount - 10) * alignCount - 55;
                if (version >= 7) result -= 36;
            }
            return result;
        }

        private static int DataCodewords(int version) =>
            RawDataModules(version) / 8 - EccCodewordsPerBlock[version] * EccBlockCount[version];

        private static void AppendBits(List<bool> bits, int value, int length)
        {
            for (int i = length - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
        }

        private static byte[] AddEccAndInterleave(byte[] data, int version)
        {
            int blockCount = EccBlockCount[version];
            int eccLength = EccCodewordsPerBlock[version];
            int rawCodewords = RawDataModules(version) / 8;
            int shortBlockCount = blockCount - rawCodewords % blockCount;
            int shortBlockLength = rawCodewords / blockCount;

            var blocks = new byte[blockCount][];
            byte[] divisor = ReedSolomonDivisor(eccLength);
            for (int i = 0, k = 0; i < blockCount; i++)
            {
                int dataLength = shortBlockLength - eccLength + (i < shortBlockCount ? 0 : 1);
                var block = new byte[shortBlockLength + 1];
                Array.Copy(data, k, block, 0, dataLength);
                byte[] ecc = ReedSolomonRemainder(data, k, dataLength, divisor);
                Array.Copy(ecc, 0, block, block.Length - eccLength, eccLength);
                k += dataLength;
                blocks[i] = block;
            }

            var result = new byte[rawCodewords];
            for (int i = 0, k = 0; i < blocks[0].Length; i++)
            {
                for (int j = 0; j < blockCount; j++)
                {
                    // Short blocks have one padding byte that is not part of the symbol.
                    if (i != shortBlockLength - eccLength || j >= shortBlockCount)
                        result[k++] = blocks[j][i];
                }
            }
            return result;
        }

        private static byte[] ReedSolomonDivisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = (byte)GfMultiply(result[j], root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = GfMultiply(root, 0x02);
            }
            return result;
        }

        private static byte[] ReedSolomonRemainder(byte[] data, int offset, int length, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            for (int n = 0; n < length; n++)
            {
                int factor = data[offset + n] ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++) result[i] ^= (byte)GfMultiply(divisor[i], factor);
            }
            return result;
        }

        private static int GfMultiply(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return z;
        }

        private sealed class Symbol
        {
            private readonly int version;
            private readonly int size;
            private readonly bool[,] isFunction;

            public bool[,] Modules { get; }

            public Symbol(int version)
            {
                this.version = version;
                size = version * 4 + 17;
                Modules = new bool[size, size];
                isFunction = new bool[size, size];
            }

            public void DrawFunctionPatterns()
            {
                for (int i = 0; i < size; i++)
                {
                    SetFunction(6, i, i % 2 == 0);
                    SetFunction(i, 6, i % 2 == 0);
                }

                DrawFinder(3, 3);
                DrawFinder(size - 4, 3);
                DrawFinder(3, size - 4);

                int[] align = AlignmentPositions();
                int last = align.Length - 1;
                for (int i = 0; i < align.Length; i++)
                {
                    for (int j = 0; j < align.Length; j++)
                    {
                        // Skip the three corners that already hold finder patterns.
                        if ((i == 0 && j == 0) || (i == 0 && j == last) || (i == last && j == 0)) continue;
                        DrawAlignment(align[i], align[j]);
                    }
                }

                DrawFormatBits(0); // reserved now, overwritten once the mask is chosen
                DrawVersion();
            }

            public void DrawCodewords(byte[] data)
            {
                int i = 0;
                for (int right = size - 1; right >= 1; right -= 2)
                {
                    if (right == 6) right = 5; // skip the vertical timing column
                    for (int vert = 0; vert < size; vert++)
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            int x = right - j;
                            bool upward = ((right + 1) & 2) == 0;
                            int y = upward ? size - 1 - vert : vert;
                            if (isFunction[y, x] || i >= data.Length * 8) continue;
                            Modules[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                            i++;
                        }
                    }
                }
            }

            public void ApplyBestMask()
            {
                int bestMask = 0;
                int bestPenalty = int.MaxValue;
                for (int mask = 0; mask < 8; mask++)
                {
                    ApplyMask(mask);
                    DrawFormatBits(mask);
                    int penalty = Penalty();
                    if (penalty < bestPenalty)
                    {
                        bestMask = mask;
                        bestPenalty = penalty;
                    }
                    ApplyMask(mask); // XOR again to undo
                }
                ApplyMask(bestMask);
                DrawFormatBits(bestMask);
            }

            private void SetFunction(int x, int y, bool dark)
            {
                Modules[y, x] = dark;
                isFunction[y, x] = true;
            }

            private void DrawFinder(int cx, int cy)
            {
                for (int dy = -4; dy <= 4; dy++)
                {
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        int x = cx + dx, y = cy + dy;
                        if (x >= 0 && x < size && y >= 0 && y < size) SetFunction(x, y, distance != 2 && distance != 4);
                    }
                }
            }

            private void DrawAlignment(int cx, int cy)
            {
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        SetFunction(cx + dx, cy + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }

            private int[] AlignmentPositions()
            {
                if (version == 1) return Array.Empty<int>();
                int count = version / 7 + 2;
                int step = (version * 8 + count * 3 + 5) / (count * 4 - 4) * 2;
                var result = new int[count];
                result[0] = 6;
                for (int i = count - 1, pos = size - 7; i >= 1; i--, pos -= step) result[i] = pos;
                return result;
            }

            private void DrawFormatBits(int mask)
            {
                int data = EccFormatBits << 3 | mask;
                int rem = data;
                for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
                int bits = (data << 10 | rem) ^ 0x5412;

                for (int i = 0; i <= 5; i++) SetFunction(8, i, Bit(bits, i));
                SetFunction(8, 7, Bit(bits, 6));
                SetFunction(8, 8, Bit(bits, 7));
                SetFunction(7, 8, Bit(bits, 8));
                for (int i = 9; i < 15; i++) SetFunction(14 - i, 8, Bit(bits, i));

                for (int i = 0; i < 8; i++) SetFunction(size - 1 - i, 8, Bit(bits, i));
                for (int i = 8; i < 15; i++) SetFunction(8, size - 15 + i, Bit(bits, i));
                SetFunction(8, size - 8, true); // always dark
            }

            private void DrawVersion()
            {
                if (version < 7) return;
                int rem = version;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int bits = version << 12 | rem;
                for (int i = 0; i < 18; i++)
                {
                    bool bit = Bit(bits, i);
                    int a = size - 11 + i % 3;
                    int b = i / 3;
                    SetFunction(a, b, bit);
                    SetFunction(b, a, bit);
                }
            }

            private void ApplyMask(int mask)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (isFunction[y, x]) continue;
                        bool invert = mask switch
                        {
                            0 => (x + y) % 2 == 0,
                            1 => y % 2 == 0,
                            2 => x % 3 == 0,
                            3 => (x + y) % 3 == 0,
                            4 => (x / 3 + y / 2) % 2 == 0,
                            5 => x * y % 2 + x * y % 3 == 0,
                            6 => (x * y % 2 + x * y % 3) % 2 == 0,
                            _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                        };
                        if (invert) Modules[y, x] = !Modules[y, x];
                    }
                }
            }

            private int Penalty()
            {
                int result = 0;
                var history = new int[7];

                for (int line = 0; line < size; line++)
                {
                    result += LinePenalty(line, true, history);
                    result += LinePenalty(line, false, history);
                }

                for (int y = 0; y < size - 1; y++)
                {
                    for (int x = 0; x < size - 1; x++)
                    {
                        bool color = Modules[y, x];
                        if (color == Modules[y, x + 1] && color == Modules[y + 1, x] && color == Modules[y + 1, x + 1])
                            result += PenaltyN2;
                    }
                }

                int dark = 0;
                foreach (bool module in Modules) if (module) dark++;
                int total = size * size;
                int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
                return result + k * PenaltyN4;
            }

            // Runs of the same color and finder-like patterns along one row or column.
            private int LinePenalty(int line, bool isRow, int[] history)
            {
                int result = 0;
                bool runColor = false;
                int runLength = 0;
                Array.Clear(history, 0, history.Length);

                for (int i = 0; i < size; i++)
                {
                    bool module = isRow ? Modules[line, i] : Modules[i, line];
                    if (module == runColor)
                    {
                        runLength++;
                        if (runLength == 5) result += PenaltyN1;
                        else if (runLength > 5) result++;
                    }
                    else
                    {
                        AddHistory(runLength, history);
                        if (!runColor) result += CountFinderPatterns(history) * PenaltyN3;
                        runColor = module;
                        runLength = 1;
                    }
                }

                if (runColor)
                {
                    AddHistory(runLength, history);
                    runLength = 0;
                }
                AddHistory(runLength + size, history);
                return result + CountFinderPatterns(history) * PenaltyN3;
            }

            private void AddHistory(int runLength, int[] history)
            {
                if (history[0] == 0) runLength += size; // light border before the first run
                Array.Copy(history, 0, history, 1, history.Length - 1);
                history[0] = runLength;
            }

            private static int CountFinderPatterns(int[] h)
            {
                int n = h[1];
                bool core = n > 0 && h[2] == n && h[3] == n * 3 && h[4] == n && h[5] == n;
                return (core && h[0] >= n * 4 && h[6] >= n ? 1 : 0) + (core && h[6] >= n * 4 && h[0] >= n ? 1 : 0);
            }

            private static bool Bit(int value, int index) => ((value >> index) & 1) != 0;
        }
    }
}
