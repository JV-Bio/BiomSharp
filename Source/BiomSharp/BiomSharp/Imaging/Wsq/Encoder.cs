// BiomSharp: Copyright (c) Businessware Architects
// Licensed under the MIT License
// See: https://biomsharp.github.io/BiomSharp/LICENSE.txt

using BiomSharp.Imaging.Wsq.Segment;
using BiomSharp.Imaging.Wsq.Tree;
using System.Buffers;

namespace BiomSharp.Imaging.Wsq
{
    internal class Encoder
    {
        public Segmenter? Segmenter { get; private set; }
        private WsqPixelBuffer<short>? quantized;
        private int[]? quantizedSizes;
        private IList<Huffman.Encoder.BlockParms>? blockParms;
        private readonly ArrayPool<float> floatPool;
        private readonly ArrayPool<short> shortPool;

        internal Encoder(Segmenter segmenter)
            : this(segmenter, ArrayPool<float>.Shared)
        {
        }

        internal Encoder(Segmenter segmenter, ArrayPool<float> floatPool)
            : this(segmenter, floatPool, ArrayPool<short>.Shared)
        {
        }

        internal Encoder(Segmenter segmenter, ArrayPool<float> floatPool, ArrayPool<short> shortPool)
        {
            ArgumentNullException.ThrowIfNull(floatPool);
            ArgumentNullException.ThrowIfNull(shortPool);
            this.floatPool = floatPool;
            this.shortPool = shortPool;
            Segmenter = segmenter;
            Segmenter.SegmentWrite += OnSegmentWrite;
        }

        private static void Fill8bppFloat(
            SimpleBitmap rawImage,
            WsqPixelBuffer<float> fib,
            out float shift,
            out float scale,
            byte black,
            byte white)
        {
            int cnt;
            long sum, ovf;
            float lod, hid;
            sum = 0;
            ovf = 0;
            int low = white;
            int high = black;
            byte[] pixels = rawImage.Pixels;

            for (cnt = 0; cnt < pixels.Length; cnt++)
            {
                int raw = pixels[cnt] & 0xFF;
                if (raw > high)
                {
                    high = raw;
                }
                if (raw < low)
                {
                    low = raw;
                }
                sum += raw;
                if (sum < ovf)
                {
                    throw new WsqCodecException(
                        "Image data byte-to-float overflow, input too big");
                }
                ovf = sum;
            }
            shift = (float)sum / pixels.Length;
            lod = shift - low;
            hid = high - shift;
            if (lod >= hid)
            {
                scale = lod;
            }
            else
            {
                scale = hid;
            }
            scale /= 128f;
            Span<float> fip = fib.Pixels.AsSpan(0, fib.Length);
            for (cnt = 0; cnt < pixels.Length; cnt++)
            {
                fip[cnt] = ((pixels[cnt] & 0xFF) - shift) / scale;
            }
            // Preserve the zero-filled remainder previously supplied by a new array.
            fip[pixels.Length..].Clear();
        }

        public void Encode(
            SimpleBitmap rawImage,
            bool writeNistHeader,
            float bitrate,
            byte black,
            byte white,
            string filterName,
            float[]? dttL0,
            float[]? dttL1,
            bool packedDht,
            int resolution = -1,
            IEnumerable<string>? comments = null)
        {
            if (rawImage != null)
            {
                if (Segmenter != null)
                {
                    Filter filter = Filter.Odd7x9;
                    if (!string.IsNullOrEmpty(filterName))
                    {
                        if (filterName.ToLower() == "7x9")
                        {
                            filter = Filter.Odd7x9;
                        }
                        else if (filterName.ToLower() == "8x8")
                        {
                            filter = Filter.Even8x8;
                        }
                        else
                        {
                            throw new WsqCodecException(
                                $"Built-in DTT filter '{filterName}' not defined");
                        }
                    }
                    else if (dttL1 != null && dttL1.Length > 0 &&
                        dttL0 != null && dttL0.Length > 0)
                    {
                        filter = Filter.Create(dttL0, dttL1);
                    }

                    if (rawImage.Width > ushort.MaxValue || rawImage.Height > ushort.MaxValue)
                    {
                        throw new WsqCodecException("Image width/height > 65535 pixels");
                    }

                    Segmenter.AddWriteSegment(BaseSegment.CreateInstance(Marker.SOI));
                    if (writeNistHeader)
                    {
                        var wsqHeader = new WsqHeader(
                            rawImage.Width,
                            rawImage.Height,
                            8,
                            rawImage.Resolution > 0 ? rawImage.Resolution : resolution > 0 ? resolution : -1,
                            bitrate);
                        Segmenter.AddWriteSegment(wsqHeader.Serialize());
                    }

                    if (comments != null && comments.Any())
                    {
                        foreach (string comment in comments)
                        {
                            Segmenter.AddWriteSegment(new Com(comment));
                        }
                    }

                    Segmenter.AddWriteSegment(new Dtt(filter));
                    var quantizer = Quantizer.Create(rawImage.Width, rawImage.Height);
                    short[]? rentedShorts = null;
                    try
                    {
                        float shift, scale;
                        int[] quantSizes;
                        float[] rented = floatPool.Rent(checked(rawImage.Width * rawImage.Height));
                        try
                        {
                            var fImage = new WsqPixelBuffer<float>(rawImage.Width, rawImage.Height, rented);
                            Fill8bppFloat(rawImage, fImage, out shift, out scale, 0, 255);
                            _ = Transformer.Decompose(fImage, quantizer, filter);
                            var dqt = new Dqt(bitrate);
                            Segmenter.AddWriteSegment(dqt);
                            rentedShorts = shortPool.Rent(fImage.Length);
                            quantized = new WsqPixelBuffer<short>(fImage.Width, fImage.Height, rentedShorts);
                            quantizer.Quantize(out quantSizes, fImage, dqt, quantized);
                        }
                        finally
                        {
                            // Huffman encoding only needs the quantized short image.
                            floatPool.Return(rented, clearArray: true);
                        }

                        quantizedSizes = quantSizes;
                        var sof = new Sof(
                            (ushort)rawImage.Height, (ushort)rawImage.Width,
                            shift, scale, black, white, 2, Implementer.Id);
                        Segmenter.AddWriteSegment(sof);
                        blockParms = Huffman.Encoder.Encode(quantized, quantSizes);
                        if (blockParms.Count != 2)
                        {
                            throw new WsqCodecException("Huffman block parms count invalid");
                        }

                        if (packedDht)
                        {
                            var dht = new Dht(0, blockParms[0].Bits.Span, blockParms[0].Values.Span);
                            Segmenter.AddWriteSegment(dht);
                            Segmenter.AddWriteSegment(new Sob(0));
                            dht.AddTable(new DhtTable(1, blockParms[1].Bits.Span, blockParms[1].Values.Span));
                            Segmenter.AddWriteSegment(new Sob(1));
                        }
                        else
                        {
                            Segmenter.AddWriteSegment(new Dht(0, blockParms[0].Bits.Span, blockParms[0].Values.Span));
                            Segmenter.AddWriteSegment(new Sob(0));
                            Segmenter.AddWriteSegment(new Dht(1, blockParms[1].Bits.Span, blockParms[1].Values.Span));
                            Segmenter.AddWriteSegment(new Sob(1));
                        }

                        Segmenter.AddWriteSegment(new Sob(1));
                        Segmenter.AddWriteSegment(BaseSegment.CreateInstance(Marker.EOI));
                        Segmenter.WriteAll();
                    }
                    finally
                    {
                        quantized = null;
                        quantizedSizes = null;
                        blockParms = null;
                        if (rentedShorts != null)
                        {
                            shortPool.Return(rentedShorts, clearArray: true);
                        }
                    }
                }
                else
                {
                    throw new WsqCodecException("Encode segmenter is null");
                }
            }
            else
            {
                throw new WsqCodecException("Raw image is null");
            }
        }

        private void WriteBlock(int index)
        {
            if (Segmenter != null && Segmenter.Writer != null)
            {
                if (quantized != null
                    &&
                    quantizedSizes != null && quantizedSizes.Length >= 3
                    &&
                    blockParms != null && blockParms.Count >= 2)
                {
                    if (index == 0)
                    {
                        Huffman.Encoder.CompressBlock(
                            Segmenter.Writer, quantized, 0, quantizedSizes[0], blockParms[0].Codes);
                    }
                    else if (index == 1)
                    {
                        Huffman.Encoder.CompressBlock(
                            Segmenter.Writer, quantized, quantizedSizes[0], quantizedSizes[1],
                            blockParms[1].Codes);
                    }
                    else
                    {
                        Huffman.Encoder.CompressBlock(
                            Segmenter.Writer, quantized, quantizedSizes[0] + quantizedSizes[1],
                            quantizedSizes[2], blockParms[1].Codes);
                    }
                }
                else
                {
                    throw new WsqCodecException(
                        "Encode quantizer is null or invalid");
                }
            }
            else
            {
                throw new WsqCodecException(
                    "Encode segmenter/writer is null or invalid");
            }
        }

        private void OnSegmentWrite(Segmenter segmenter, BaseSegment lastWrite)
        {
            switch (lastWrite.Marker)
            {
                case Marker.SOB:
                    int index = segmenter.IndexOf((Sob)lastWrite);
                    WriteBlock(index);
                    break;
                case Marker.None:
                    break;
                case Marker.SOI:
                    break;
                case Marker.EOI:
                    break;
                case Marker.SOF:
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
