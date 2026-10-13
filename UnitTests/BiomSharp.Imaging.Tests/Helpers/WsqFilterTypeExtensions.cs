using BiomSharp.Imaging.Wsq;
using BiomSharp.Imaging.Wsq.Tree;

namespace BiomSharp.Imaging.Tests.Helpers;

internal static class WsqFilterTypeExtensions
{
    internal static Filter GetFilter(this WsqFilterType filterType)
    {
        return filterType switch
        {
            WsqFilterType.Odd7x9 => Filter.Odd7x9,
            WsqFilterType.Even8x8 => Filter.Even8x8,
            _ => throw new ArgumentOutOfRangeException(nameof(filterType), filterType, "Unsupported built-in filter.")
        };
    }
}
