// BiomSharp: Copyright (c) Businessware Architects
// Licensed under the MIT License
// See: https://biomsharp.github.io/BiomSharp/LICENSE.txt

using System.Buffers.Binary;
using System.Text;

namespace BiomSharp.Imaging.Wsq.IO
{

    /// <summary>
    /// WSQ is big endian encoded. All reads will use from the stream using big endian.
    /// </summary>
    internal class EndianBinaryReader : IDisposable
    {
        public Stream BaseStream { get; private set; }

        public EndianBinaryReader(Stream stream) => BaseStream = stream;

        private void ReadFully(Span<byte> buffer)
        {
            while (!buffer.IsEmpty)
            {
                int count = BaseStream.Read(buffer);
                if (count == 0)
                {
                    throw new EndOfStreamException();
                }

                buffer = buffer[count..];
            }
        }


        public byte ReadByte()
        {
            int value = BaseStream.ReadByte();
            if (value < 0)
            {
                throw new EndOfStreamException();
            }
            return (byte)value;
        }

        public short ReadInt16()
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            ReadFully(buffer);
            return BinaryPrimitives.ReadInt16BigEndian(buffer);
        }

        public ushort ReadUInt16()
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            ReadFully(buffer);
            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        public int ReadInt32()
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            ReadFully(buffer);
            return BinaryPrimitives.ReadInt32BigEndian(buffer);
        }

        public uint ReadUInt32()
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            ReadFully(buffer);
            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        public string ReadString(int len)
        {
            // Lenght decides if the buffer will be initialized on the heap or stack.
            // Heap allocation for short comments.
            // Stack allocation for long comments.
            Span<byte> buffer = len <= 256
                ? stackalloc byte[len]
                : new byte[len];

            ReadFully(buffer);
            return Encoding.ASCII.GetString(buffer);
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
