// BiomSharp: Copyright (c) Businessware Architects
// Licensed under the MIT License
// See: https://biomsharp.github.io/BiomSharp/LICENSE.txt

using BiomSharp.Imaging.Wsq.Segment;
using BiomSharp.Imaging.Wsq.Tree;

namespace BiomSharp.Imaging.Wsq
{
    internal class Quantizer
    {
        public const int StartSubband2 = 19;
        public const int StartSubband3 = 52;
        public const int SubbandCount = 60;
        public const int StartSizeRegion2 = 4;
        public const int StartSizeRegion3 = 51;
        public const float VarThreshold = 1.01F;
        public WTree? WTree { get; private set; }
        public QTree? QTree { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private Quantizer() { }

        public static Quantizer Create(Sof sof)
        {
            var quantizer = new Quantizer
            {
                Width = sof.X,
                Height = sof.Y,
                WTree = new WTree(sof.X, sof.Y)
            };
            quantizer.QTree = new QTree(quantizer.WTree);
            return quantizer;
        }

        public static Quantizer Create(int width, int height)
        {
            var quantizer = new Quantizer
            {
                WTree = new WTree(width, height)
            };
            quantizer.QTree = new QTree(quantizer.WTree);
            return quantizer;
        }

        private void SetVariance(Dqt dqt, WsqPixelBuffer<float> image)
        {
            ReadOnlySpan<float> pixels = image.Pixels.AsSpan(0, image.Length);
            int fpIdx;
            int lenx, leny;
            int skipx, skipy;
            int row, col;
            float ssq;
            float sum2;
            float sumPix;
            float vsum = 0F;

            if (QTree != null)
            {
                for (int cvr = 0; cvr < 4; cvr++)
                {
                    fpIdx = (QTree[cvr].Y * image.Width) + QTree[cvr].X;
                    ssq = 0F;
                    sumPix = 0F;

                    skipx = QTree[cvr].LenX / 8;
                    skipy = 9 * QTree[cvr].LenY / 32;

                    lenx = 3 * QTree[cvr].LenX / 4;
                    leny = 7 * QTree[cvr].LenY / 16;

                    fpIdx += (skipy * image.Width) + skipx;
                    for (row = 0; row < leny; row++, fpIdx += image.Width - lenx)
                    {
                        for (col = 0; col < lenx; col++)
                        {
                            sumPix += pixels[fpIdx];
                            ssq += pixels[fpIdx] * pixels[fpIdx];
                            fpIdx++;
                        }
                    }
                    sum2 = sumPix * sumPix / (lenx * leny);
                    dqt.Var[cvr] = (ssq - sum2) / ((lenx * leny) - 1F);
                    vsum += dqt.Var[cvr];
                }

                if (vsum < 20000F)
                {
                    for (int cvr = 0; cvr < SubbandCount; cvr++)
                    {
                        fpIdx = (QTree[cvr].Y * image.Width) + QTree[cvr].X;
                        ssq = 0F;
                        sumPix = 0F;

                        lenx = QTree[cvr].LenX;
                        leny = QTree[cvr].LenY;

                        for (row = 0; row < leny; row++, fpIdx += image.Width - lenx)
                        {
                            for (col = 0; col < lenx; col++)
                            {
                                sumPix += pixels[fpIdx];
                                ssq += pixels[fpIdx] * pixels[fpIdx];
                                fpIdx++;
                            }
                        }
                        sum2 = sumPix * sumPix / (lenx * leny);
                        dqt.Var[cvr] = (ssq - sum2) / ((lenx * leny) - 1F);
                    }
                }
                else
                {
                    for (int cvr = 4; cvr < SubbandCount; cvr++)
                    {
                        fpIdx = (QTree[cvr].Y * image.Width) + QTree[cvr].X;
                        ssq = 0F;
                        sumPix = 0F;

                        skipx = QTree[cvr].LenX / 8;
                        skipy = 9 * QTree[cvr].LenY / 32;

                        lenx = 3 * QTree[cvr].LenX / 4;
                        leny = 7 * QTree[cvr].LenY / 16;

                        fpIdx += (skipy * image.Width) + skipx;
                        for (row = 0; row < leny; row++, fpIdx += image.Width - lenx)
                        {
                            for (col = 0; col < lenx; col++)
                            {
                                sumPix += pixels[fpIdx];
                                ssq += pixels[fpIdx] * pixels[fpIdx];
                                fpIdx++;
                            }
                        }
                        sum2 = sumPix * sumPix / (lenx * leny);
                        dqt.Var[cvr] = (ssq - sum2) / ((lenx * leny) - 1F);
                    }
                }
            }
            else
            {
                throw new WsqCodecException("QTree is null or invalid");
            }
        }

        private void QuantizeImage(out int[] quantSizes, WsqPixelBuffer<float> fImage, Dqt dqt,
            WsqPixelBuffer<short> sImage)
        {
            if (QTree != null && WTree != null)
            {
                Span<float> a = stackalloc float[SubbandCount];
                a.Clear();
                for (int i = 0; i < StartSubband3; i++)
                {
                    a[i] = 1F;
                }

                a[StartSubband3] = 1.32F;
                a[StartSubband3 + 1] = 1.08F;
                a[StartSubband3 + 2] = 1.42F;
                a[StartSubband3 + 3] = 1.08F;
                a[StartSubband3 + 4] = 1.32F;
                a[StartSubband3 + 5] = 1.42F;
                a[StartSubband3 + 6] = 1.08F;
                a[StartSubband3 + 7] = 1.08F;

                for (int cnt = 0; cnt < SubbandCount; cnt++)
                {
                    if (dqt.Var[cnt] < VarThreshold)
                    {
                        dqt.DqtQ[cnt] = 0F;
                    }
                    else
                    {
                        if (cnt < StartSizeRegion2)
                        {
                            dqt.DqtQ[cnt] = 1F;
                        }
                        else
                        {
                            dqt.DqtQ[cnt] = 10F / (a[cnt] * (float)System.Math.Log(dqt.Var[cnt]));
                        }
                    }
                }

                Span<float> m = stackalloc float[SubbandCount];
                float m1 = 1F / 1024F;
                float m2 = 1F / 256F;
                float m3 = 1F / 16F;
                for (int i = 0; i < StartSizeRegion2; i++)
                {
                    m[i] = m1;
                }

                for (int i = StartSizeRegion2; i < SubbandCount; i++)
                {
                    m[i] = m2;
                }

                for (int i = StartSizeRegion3; i < SubbandCount; i++)
                {
                    m[i] = m3;
                }

                int k0Len = 0;
                Span<int> k0 = stackalloc int[SubbandCount];
                Span<int> k1 = stackalloc int[SubbandCount];
                Span<float> sigma = stackalloc float[SubbandCount];
                k0.Clear();
                k1.Clear();
                sigma.Clear();
                for (int cnt = 0; cnt < SubbandCount; cnt++)
                {
                    if (dqt.Var[cnt] >= VarThreshold)
                    {
                        k0[k0Len] = cnt;
                        k1[k0Len++] = cnt;
                        sigma[cnt] = (float)System.Math.Sqrt(dqt.Var[cnt]);
                    }
                }

                int nkIndex;
                int kIndex = 0;
                int kLen = k0Len;
                float q;
                Span<bool> nonPos = stackalloc bool[SubbandCount];
                while (true)
                {
                    float s = 0F;
                    for (int i = 0; i < kLen; i++)
                    {
                        s += m[k1[kIndex + i]];
                    }
                    float p = 1F;
                    for (int i = 0; i < kLen; i++)
                    {
                        p *= (float)System.Math.Pow(
                            sigma[k1[kIndex + i]] / dqt.DqtQ[k1[kIndex + i]], m[k1[kIndex + i]]);
                    }
                    q = (float)System.Math.Pow(
                        2, (dqt.R / s) - 1F) / 2.5F / (float)System.Math.Pow(p, 1F / s);
                    nonPos.Clear();
                    int npLen = 0;
                    for (int i = 0; i < kLen; i++)
                    {
                        if (dqt.DqtQ[k1[kIndex + i]] / q >= 5F * sigma[k1[kIndex + i]])
                        {
                            nonPos[k1[kIndex + i]] = true;
                            npLen++;
                        }
                    }

                    if (npLen == 0)
                    {
                        break;
                    }

                    nkIndex = 0;
                    int nkLen = 0;
                    for (int i = 0; i < kLen; i++)
                    {
                        if (!nonPos[k1[kIndex + i]])
                        {
                            k1[nkIndex + nkLen++] = k1[kIndex + i];
                        }
                    }

                    kIndex = nkIndex;
                    kLen = nkLen;
                }

                nkIndex = 0;

                k1.Clear();

                for (int i = 0; i < k0Len; i++)
                {
                    k1[nkIndex + k0[i]] = 1;
                }
                for (int cnt = 0; cnt < SubbandCount; cnt++)
                {
                    if (k1[nkIndex + cnt] != 0)
                    {
                        dqt.DqtQ[cnt] /= q;
                    }
                    else
                    {
                        dqt.DqtQ[cnt] = 0F;
                    }
                    dqt.DqtZ[cnt] = 1.2F * dqt.DqtQ[cnt];
                }

                Span<short> spix = sImage.Pixels.AsSpan(0, sImage.Length);
                int sIdx = 0;
                ReadOnlySpan<float> fpix = fImage.Pixels.AsSpan(0, fImage.Length);
                int row, col;
                float zbin;
                for (int cnt = 0; cnt < SubbandCount; cnt++)
                {
                    int fIdx = (QTree[cnt].Y * fImage.Width) + QTree[cnt].X;

                    if (dqt.DqtQ[cnt] != 0F)
                    {

                        zbin = dqt.DqtZ[cnt] / 2F;

                        for (row = 0; row < QTree[cnt].LenY; row++, fIdx += fImage.Width - QTree[cnt].LenX)
                        {
                            for (col = 0; col < QTree[cnt].LenX; col++)
                            {
                                if (-zbin <= fpix[fIdx] && fpix[fIdx] <= zbin)
                                {
                                    spix[sIdx] = 0;
                                }
                                else if (fpix[fIdx] > 0F)
                                {
                                    spix[sIdx] = (short)(((fpix[fIdx] - zbin) / dqt.DqtQ[cnt]) + 1F);
                                }
                                else
                                {
                                    spix[sIdx] = (short)(((fpix[fIdx] + zbin) / dqt.DqtQ[cnt]) - 1F);
                                }
                                sIdx++;
                                fIdx++;
                            }
                        }
                    }
                }

                int qSize = sIdx;
                quantSizes = new int[3]
                {
                WTree[14].LenX * WTree[14].LenY,
                (WTree[5].LenY * WTree[1].LenX) + (WTree[4].LenX * WTree[4].LenY),
                (WTree[2].LenX * WTree[2].LenY) + (WTree[3].LenX * WTree[3].LenY),
                };
                for (int node = 0; node < StartSubband2; node++)
                {
                    if (dqt.DqtQ[node] == 0F)
                    {
                        quantSizes[0] -= QTree[node].LenX * QTree[node].LenY;
                    }
                }
                for (int node = StartSubband2; node < StartSubband3; node++)
                {
                    if (dqt.DqtQ[node] == 0F)
                    {
                        quantSizes[1] -= QTree[node].LenX * QTree[node].LenY;
                    }
                }
                for (int node = StartSubband3; node < SubbandCount; node++)
                {
                    if (dqt.DqtQ[node] == 0F)
                    {
                        quantSizes[2] -= QTree[node].LenX * QTree[node].LenY;
                    }
                }
                if (qSize != quantSizes[0] + quantSizes[1] + quantSizes[2])
                {
                    throw new WsqCodecException("Quantization block sizes invalid");
                }
            }
            else
            {
                throw new WsqCodecException("QTree/WTree is null or invalid");
            }
        }

        public void Unquantize(WsqPixelBuffer<int> huffDecoded, Dqt dqt, WsqPixelBuffer<float> fImage)
        {
            if (fImage.Width != Width || fImage.Height != Height)
            {
                throw new ArgumentException("Unquantized image dimensions must match the quantizer.", nameof(fImage));
            }
            if (huffDecoded.Width != Width || huffDecoded.Height != Height)
            {
                throw new ArgumentException("Coefficient image dimensions must match the quantizer.", nameof(huffDecoded));
            }
            if (QTree != null)
            {
                int ip = 0;
                Span<float> pix = fImage.Pixels.AsSpan(0, fImage.Length);
                // Inactive subbands are not written by the loop below.
                pix.Clear();
                ReadOnlySpan<int> hdc = huffDecoded.Pixels.AsSpan(0, huffDecoded.Length);

                for (int cnt = 0; cnt < SubbandCount; cnt++)
                {
                    if (dqt.DqtQ[cnt] != 0F)
                    {
                        int fp = (QTree[cnt].Y * fImage.Width) + QTree[cnt].X;
                        for (int row = 0; row < QTree[cnt].LenY; row++, fp += fImage.Width - QTree[cnt].LenX)
                        {
                            for (int col = 0; col < QTree[cnt].LenX; col++)
                            {
                                if (hdc[ip] == 0)
                                {
                                    pix[fp] = 0f;
                                }
                                else if (hdc[ip] > 0)
                                {
                                    pix[fp] = (dqt.DqtQ[cnt] * (hdc[ip] - dqt.Cf)) + (dqt.DqtZ[cnt] / 2F);
                                }
                                else if (hdc[ip] < 0)
                                {
                                    pix[fp] = (dqt.DqtQ[cnt] * (hdc[ip] + dqt.Cf)) - (dqt.DqtZ[cnt] / 2F);
                                }
                                else
                                {
                                    throw new WsqCodecException("Invalid quantization pixel value");
                                }
                                ++fp;
                                ++ip;
                            }
                        }
                    }
                }
            }
            else
            {
                throw new WsqCodecException("QTree is null or invalid");
            }
        }

        public void Quantize(out int[] quantSizes, WsqPixelBuffer<float> fImage, Dqt dqt,
            WsqPixelBuffer<short> sImage)
        {
            if (sImage.Width != fImage.Width || sImage.Height != fImage.Height)
            {
                throw new ArgumentException("Quantized image dimensions must match the float image.", nameof(sImage));
            }
            SetVariance(dqt, fImage);
            QuantizeImage(out quantSizes, fImage, dqt, sImage);
        }
    }
}
