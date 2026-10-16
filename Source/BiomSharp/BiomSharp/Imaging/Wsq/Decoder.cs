// BiomSharp: Copyright (c) Businessware Architects
// Licensed under the MIT License
// See: https://biomsharp.github.io/BiomSharp/LICENSE.txt

using System.Buffers;
using BiomSharp.Imaging.Wsq.Segment;

namespace BiomSharp.Imaging.Wsq
{
    internal class Decoder : IDisposable
    {
        private Huffman.Decoder? huffmanDecoder;
        private WsqPixelBuffer<int>? huffDecoded;
        private int[]? rentedCoefficients;
        private readonly Segmenter segmenter;
        private readonly ArrayPool<int> coefficientPool;
        private readonly ArrayPool<float> floatPool;

        public SimpleBitmap? Decoded { get; private set; }


        public Decoder(Segmenter segmenter)
            : this(segmenter, ArrayPool<int>.Shared, ArrayPool<float>.Shared)
        {
        }

        internal Decoder(Segmenter segmenter, ArrayPool<int> coefficientPool, ArrayPool<float> floatPool)
        {
            ArgumentNullException.ThrowIfNull(segmenter);
            ArgumentNullException.ThrowIfNull(coefficientPool);
            ArgumentNullException.ThrowIfNull(floatPool);
            this.segmenter = segmenter;
            this.coefficientPool = coefficientPool;
            this.floatPool = floatPool;
            segmenter.SegmentRead += OnSegmentRead;
        }

        private void ReturnCoefficients()
        {
            int[]? rented = rentedCoefficients;
            rentedCoefficients = null;
            huffDecoded = null;
            huffmanDecoder = null;
            if (rented != null)
            {
                coefficientPool.Return(rented, clearArray: true);
            }
        }

        public void Dispose()
        {
            segmenter.SegmentRead -= OnSegmentRead;
            ReturnCoefficients();
        }

        private static SimpleBitmap Create8bppRaw(
            ReadOnlySpan<float> img,
            int width,
            int height,
            float shift,
            float scale,
            byte black,
            byte white)
        {
            var rawImage = SimpleBitmap.Gray8(new byte[img.Length], width, height, -1);
            byte[] pixels = rawImage.Pixels;
            for (int idx = 0; idx < img.Length; idx++)
            {
                float pixel = (scale * img[idx]) + shift + 0.5f;
                if (pixel < black)
                {
                    pixels[idx] = black;
                }
                else if (pixel > white)
                {
                    pixels[idx] = white;
                }
                else
                {
                    pixels[idx] = (byte)pixel;
                }
            }
            return rawImage;
        }

        private void OnSegmentRead(Segmenter segmenter, BaseSegment lastRead)
        {
            switch (lastRead.Marker)
            {
                case Marker.SOF:
                    Sof sof = segmenter.First<Sof>();
                    ReturnCoefficients();
                    int length = checked(sof.X * sof.Y);
                    rentedCoefficients = coefficientPool.Rent(length);
                    rentedCoefficients.AsSpan(0, length).Clear();
                    huffmanDecoder = Huffman.Decoder.Create();
                    huffDecoded = new WsqPixelBuffer<int>(sof.X, sof.Y, rentedCoefficients);
                    break;
                case Marker.SOB:
                    DhtTable dhtTable = segmenter.DhtTableWithId(((Sob)lastRead).Td);
                    var hdt = dhtTable.GetDecodingTable();
                    if (huffmanDecoder != null
                        &&
                        segmenter.Reader != null
                        &&
                        huffDecoded != null)
                    {
                        huffmanDecoder.Decode(segmenter.Reader, huffDecoded, dhtTable, hdt);
                    }
                    else
                    {
                        throw new WsqCodecException(
                            "Huffman decoder, reader or decoded image is null or invalid");
                    }
                    break;
                case Marker.EOI:
                    sof = segmenter.First<Sof>();
                    var quantizer = Quantizer.Create(sof);
                    if (huffDecoded != null)
                    {
                        float[] rented = floatPool.Rent(huffDecoded.Length);
                        try
                        {
                            var fImage = new WsqPixelBuffer<float>(sof.X, sof.Y, rented);
                            try
                            {
                                quantizer.Unquantize(huffDecoded, segmenter.First<Dqt>(), fImage);
                            }
                            finally
                            {
                                ReturnCoefficients();
                            }
                            _ = Transformer.Construct(fImage, segmenter.First<Dtt>(), quantizer);
                            Decoded = Create8bppRaw(
                                fImage.Pixels.AsSpan(0, fImage.Length),
                                fImage.Width,
                                fImage.Height,
                                sof.Shift,
                                sof.Scale,
                                sof.A, sof.B);
                        }
                        finally
                        {
                            floatPool.Return(rented, clearArray: true);
                        }
                    }
                    else
                    {
                        throw new WsqCodecException(
                            "Huffman decoded image is null or invalid");
                    }
                    break;
                case Marker.None:
                    break;
                case Marker.SOI:
                    break;
                case Marker.DTT:
                    break;
                case Marker.DQT:
                    break;
                case Marker.DHT:
                    break;
                case Marker.DRI:
                    break;
                case Marker.COM:
                    break;
                case Marker.RST0:
                    break;
                case Marker.RST1:
                    break;
                case Marker.RST2:
                    break;
                case Marker.RST3:
                    break;
                case Marker.RST4:
                    break;
                case Marker.RST5:
                    break;
                case Marker.RST6:
                    break;
                case Marker.RST7:
                    break;
                default:
                    break;
            }
        }
    }
}
