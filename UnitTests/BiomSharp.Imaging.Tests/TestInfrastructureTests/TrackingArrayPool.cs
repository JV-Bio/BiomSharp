using System.Buffers;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

internal sealed class TrackingArrayPool<T> : ArrayPool<T> where T : unmanaged
{
    private readonly int _logicalLength;
    private bool _rented;
    private T[] _tail = Array.Empty<T>();

    internal T[] Buffer { get; }
    internal int RequestedLength { get; private set; }
    internal int RentCount { get; private set; }
    internal int ReturnCount { get; private set; }
    internal bool ClearRequested { get; private set; }
    internal bool TailWasUnchanged { get; private set; }
    internal Action? OnReturn { get; set; }
    internal Action? OnRent { get; set; }

    internal TrackingArrayPool(int logicalLength, T initialValue)
    {
        _logicalLength = logicalLength;
        Buffer = new T[logicalLength + 29];
        Array.Fill(Buffer, initialValue);
    }

    public override T[] Rent(int minimumLength)
    {
        if (_rented || minimumLength > Buffer.Length)
        {
            throw new InvalidOperationException("Invalid buffer rental.");
        }
        OnRent?.Invoke();
        _rented = true;
        _tail = Buffer.Skip(_logicalLength).ToArray();
        RequestedLength = minimumLength;
        RentCount++;
        return Buffer;
    }

    public override void Return(T[] array, bool clearArray = false)
    {
        if (!_rented || !ReferenceEquals(array, Buffer))
        {
            throw new InvalidOperationException("An unowned buffer was returned.");
        }
        _rented = false;
        ReturnCount++;
        ClearRequested = clearArray;
        TailWasUnchanged = array.Skip(_logicalLength).SequenceEqual(_tail);
        if (clearArray)
        {
            Array.Clear(array);
        }
        OnReturn?.Invoke();
    }
}
