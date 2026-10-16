// BiomSharp: Copyright (c) Businessware Architects
// Licensed under the MIT License
// See: https://biomsharp.github.io/BiomSharp/LICENSE.txt

namespace BiomSharp.Imaging.Wsq
{
    internal class WsqPixelBuffer<TPixel> where TPixel : unmanaged
    {
        public TPixel[] Pixels { get; }
        public int Length => Width * Height;
        public int Width { get; }
        public int Height { get; }

        public WsqPixelBuffer(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new TPixel[Length];
        }

        // The caller owns this storage, which may have spare capacity beyond Length.
        public WsqPixelBuffer(int width, int height, TPixel[] pixels)
        {
            ArgumentNullException.ThrowIfNull(pixels);
            Width = width;
            Height = height;
            if (pixels.Length < Length)
            {
                throw new ArgumentException("Pixel storage is smaller than the image dimensions.", nameof(pixels));
            }
            Pixels = pixels;
        }

        public TPixel this[int x, int y]
        {
            get => Pixels[(y * Width) + x];
            set => Pixels[(y * Width) + x] = value;
        }

        public TPixel this[int i]
        {
            get => Pixels[i];
            set => Pixels[i] = value;
        }
    }
}
