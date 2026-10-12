using BiomSharp.Imaging.Wsq;

namespace BiomSharp.Imaging.Tests.Models;

public sealed record EncodingTestCase(string ImageName, 
    byte[] OrignalImageData, 
   SimpleBitmap OriginalImageBitmap, 
   WsqParameters Parameters, 
   WsqCodec Codec);
