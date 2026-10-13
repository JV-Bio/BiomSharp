// BiomSharp: Copyright (c) Businessware Architects
// Licensed under the MIT License
// See: https://biomsharp.github.io/BiomSharp/LICENSE.txt

using System.Buffers.Binary;
using System.Text;

namespace BiomSharp.Imaging.Wsq.IO
{
    internal class EndianBinaryWriter : IDisposable
    {
        public Stream BaseStream { get; private set; }

        public EndianBinaryWriter(Stream stream)
        {
            BaseStream = stream;
        }

        public void Write(ReadOnlySpan<byte> bytes) => BaseStream.Write(bytes);

        public void Write(byte b) => BaseStream.WriteByte(b);

        public void Write(short s)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            BinaryPrimitives.WriteInt16BigEndian(buffer, s);
            BaseStream.Write(buffer);
        }

        public void Write(ushort u)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, u);
            BaseStream.Write(buffer);
        }

        public void Write(int i)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(buffer, i);
            BaseStream.Write(buffer);
        }

        public void Write(uint u)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, u);
            BaseStream.Write(buffer);
        }

        public void Write(string s)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(s);
            BaseStream.Write(buffer, 0, buffer.Length);
        }

        #region IDisposable implementation

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    BaseStream.Dispose();
                }
                disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
