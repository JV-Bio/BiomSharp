using BiomSharp.Imaging.Pgm;
using BiomSharp.Imaging.Tests.Models;
using BiomSharp.Imaging.Wsq;
using System.Diagnostics;

namespace BiomSharp.Imaging.Tests.Fixtures
{
    public abstract class FixtureEncodingBase<TFixture> : FixtureTestBase<TFixture> where TFixture : class
    {
        protected FixtureEncodingBase()
        { }
      

        internal EncodingTestCase ArrangeEncoding(string imageName, WsqParameters parameters)
        {
            Assert.True(Path.HasExtension(imageName) && Path.GetFileName(imageName) == imageName,
                "The input image name must include a file extension and must not contain a directory path.");

            byte[] inputBytes = ReadVerifiedInput(imageName);
            var pgm = new PgmCodec();
            using (var input = new MemoryStream(inputBytes, writable: false))
            {
                pgm.Read(input);
            }

            SimpleBitmap bitmap = pgm.ToRaw() ?? throw new InvalidDataException("PGM reader returned no image.");
            Assert.False(bitmap.IsColor);
            Assert.Equal(checked(bitmap.Width * bitmap.Height), bitmap.Pixels.Length);                   

            var codec = new WsqCodec();
            codec.FromRaw(bitmap);
            return new EncodingTestCase(imageName, inputBytes, bitmap, parameters, codec);
        }

        internal TimedEncoding EncodeToMemory(EncodingTestCase test, Barrier? start = null)
        {
            try
            {
                WsqParameters parameters = test.Parameters;
                
                var codec = new WsqCodec();     // JV TODO: WHY IS the test.Codec not used instead of creating a new one.
                codec.FromRaw(test.OriginalImageBitmap.Clone());                
                if (start != null && !start.SignalAndWait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("Parallel WSQ encoders did not reach the start barrier within 30 seconds.");
                }

                DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
                var timer = Stopwatch.StartNew();
                byte[] bytes = codec.Encode(parameters)
                    ?? throw new InvalidDataException($"WSQ encoder returned no output for {test.ImageName}.");
                timer.Stop();

                DateTimeOffset endedUtc = DateTimeOffset.UtcNow;
                return new TimedEncoding(bytes, timer.Elapsed, startedUtc, endedUtc);
            }
            finally
            {
                // A worker that fails during preparation must not leave the other workers blocked.
                start?.RemoveParticipant();
            }
        }

    }
}
