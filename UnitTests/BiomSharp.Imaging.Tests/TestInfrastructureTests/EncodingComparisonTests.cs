using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.Models;
using WsqParameters = BiomSharp.Imaging.Wsq.WsqParameters;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

[Trait("Category", "Comparator")]
public class EncodingComparisonTests
{
    [Theory]
    [InlineData(180, 0)]
    [InlineData(181, 1)]
    public void Report_writer_records_comparison_without_asserting_or_writing_wsq(
        int symbol, int mismatches)
    {
        string directory = Path.Combine(TestResources.UnitTestsDirectory, "TestResults", $"report_{Guid.NewGuid():N}");
        byte[] plain = CreateFixture((byte)symbol);
        byte[] comment = { 0xff, 0xa8, 0, 5, 65, 66, 67 };
        byte[] encodedBytes = plain[..2].Concat(comment).Concat(plain[2..]).ToArray();
        var test = CreateEncodingTestCase() with
        {
            CaseId = new TestCaseIdentity("example.pgm", "report", directory, "Encoding")
        };
        var encoded = new DataContainerWsq("encoded.wsq", encodedBytes);
        test.AddWsqContainer(ExecutableType.BiomSharp, encoded);
        try
        {
            new FixtureFullProcess().WriteComparisonReport(test);

            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "example.comparison-15.json")));
            JsonElement input = json.RootElement.GetProperty("OriginalImage");
            Assert.Equal("example.pgm", input.GetProperty("FileName").GetString());
            Assert.Equal(test.OriginalPgm.Data.LongLength, input.GetProperty("SizeInBytes").GetInt64());
            Assert.False(json.RootElement.TryGetProperty("CompressionRatio", out _));
            var entry = json.RootElement.GetProperty("EncodedComparisons").GetProperty("BiomSharp");
            string expectedRatio = ((double)test.OriginalPgm.Data.LongLength / plain.Length)
                .ToString("0.##", CultureInfo.InvariantCulture) + ":1";
            Assert.Equal(expectedRatio, entry.GetProperty("Metadata").GetProperty("CompressionRatio").GetString());
            Assert.Equal(expectedRatio, json.RootElement.GetProperty("OriginalWsq").GetProperty("Metadata").GetProperty("CompressionRatio").GetString());
            Assert.Equal(32, entry.GetProperty("Metadata").GetProperty("Width").GetInt32());
            JsonElement comparison = entry.GetProperty("ReferenceComparison").GetProperty("Structure");
            Assert.Equal(mismatches, comparison.GetProperty("Mismatches").GetInt32());
            Assert.Equal(mismatches, comparison.GetProperty("Failures").GetArrayLength());
            Assert.Single(Directory.GetFiles(directory));
            Assert.Empty(Directory.GetFiles(directory, "*.wsq"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(5, "5:1")]
    [InlineData(10, "10:1")]
    [InlineData(1.25, "1.25:1")]
    [InlineData(0.5, "0.5:1")]
    [InlineData(1.333, "1.33:1")]
    public void Metadata_formats_file_compression_ratio(double ratio, string expected)
    {
        var inspection = WsqInspection.Read(CreateFixture());
        var metadata = WsqMetadata.Create(inspection, (long)(inspection.SizeWithoutComments * ratio));
        Assert.Equal(expected, metadata.CompressionRatio);
    }

    [Fact]
    public void Wsq_writer_preserves_supplied_bytes_without_writing_a_report()
    {
        string directory = Path.Combine(TestResources.UnitTestsDirectory, "TestResults", $"wsq_{Guid.NewGuid():N}");
        byte[] bytes = CreateFixture();
        byte[] referenceBytes = CreateFixture(181);
        var test = CreateEncodingTestCase() with
        {
            CaseId = new TestCaseIdentity("example.pgm", "files", directory, "Encoding"),
            OriginalWsqData = new DataContainerWsq("reference.wsq", referenceBytes)
        };
        test.AddWsqContainer(ExecutableType.BiomSharp, new DataContainerWsq("encoded.wsq", bytes));
        try
        {
            Directory.CreateDirectory(directory);
            new FixtureFullProcess().WriteFilesToCaseDirectory(test);

            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(directory, "example.BiomSharp.encoded-15.wsq")));
            Assert.Equal(referenceBytes, File.ReadAllBytes(Path.Combine(directory, "example.original-15.wsq")));
            Assert.Equal(test.OriginalPgm.Data, File.ReadAllBytes(Path.Combine(directory, "example.original.pgm")));
            Assert.Equal(3, Directory.GetFiles(directory).Length);
            Assert.Empty(Directory.GetFiles(directory, "*.json"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Fixture_assertion_accepts_matching_wsq_data()
    {
        byte[] reference = CreateFixture();
        var fixture = new FixtureFullProcess();

        fixture.AssertEncoding(
            new DataContainerWsq("reference.wsq", reference), new DataContainerWsq("encoded.wsq", reference.ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Full_process_encoding_assertion_accepts_matching_wsq_ignoring_comments(bool addComment)
    {
        byte[] referenceBytes = CreateFixture();
        byte[] comment = { 0xff, 0xa8, 0, 5, 65, 66, 67 };
        byte[] encodedBytes = addComment
            ? referenceBytes[..2].Concat(comment).Concat(referenceBytes[2..]).ToArray()
            : referenceBytes.ToArray();
        var reference = new DataContainerWsq("reference.wsq", referenceBytes);
        var encoded = new DataContainerWsq("encoded.wsq", encodedBytes);

        new FixtureFullProcess().AssertEncoding(reference, encoded);
    }

    [Fact]
    public void Full_process_encoding_assertion_reports_coefficient_failures_with_file_names()
    {
        var reference = new DataContainerWsq("reference.wsq", CreateFixture());
        var encoded = new DataContainerWsq("encoded.wsq", CreateFixture(181));

        var error = Assert.Throws<Xunit.Sdk.TrueException>(() =>
            new FixtureFullProcess().AssertEncoding(reference, encoded));

        Assert.Contains("coefficient indices differ", error.Message);
        Assert.Contains(reference.FileName, error.Message);
        Assert.Contains(encoded.FileName, error.Message);
    }

    [Fact]
    public void Full_process_encoding_assertion_reports_all_comparison_failures()
    {
        byte[] bytes = CreateFixture();
        var reference = new DataContainerWsq("reference.wsq", bytes);
        var inspection = reference.WspInspection with
        {
            Header = reference.WspInspection.Header with { Width = 1, Encoder = 0, LowFilterLength = 8 },
            SizeWithoutComments = reference.WspInspection.SizeWithoutComments * 2
        };
        var encoded = new DataContainerWsq("encoded.wsq", bytes, inspection);

        var error = Assert.Throws<Xunit.Sdk.TrueException>(() =>
            new FixtureFullProcess().AssertEncoding(reference, encoded));

        Assert.Contains("dimensions differ", error.Message);
        Assert.Contains("Frame header Ev", error.Message);
        Assert.Contains("prescribed", error.Message);
        Assert.Contains("Compressed size differs", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Full_process_encoding_assertion_rejects_missing_container(bool missingReference)
    {
        var valid = new DataContainerWsq("valid.wsq", CreateFixture());

        Assert.Throws<Xunit.Sdk.NotNullException>(() =>
            new FixtureFullProcess().AssertEncoding(missingReference ? null! : valid, missingReference ? valid : null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Full_process_encoding_assertion_rejects_empty_data(bool emptyReference)
    {
        var valid = new DataContainerWsq("valid.wsq", CreateFixture());
        var empty = new DataContainerWsq("empty.wsq", Array.Empty<byte>());

        Assert.Throws<Xunit.Sdk.NotEmptyException>(() =>
            new FixtureFullProcess().AssertEncoding(emptyReference ? empty : valid, emptyReference ? valid : empty));
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(32, 0)]
    public void Full_process_encoding_assertion_rejects_nonpositive_reference_dimensions(int width, int height)
    {
        byte[] bytes = CreateFixture();
        var valid = new DataContainerWsq("valid.wsq", bytes);
        var inspection = valid.WspInspection with
        {
            Header = valid.WspInspection.Header with { Width = width, Height = height }
        };
        var reference = new DataContainerWsq("reference.wsq", bytes, inspection);

        var error = Assert.Throws<Xunit.Sdk.TrueException>(() =>
            new FixtureFullProcess().AssertEncoding(reference, valid));

        Assert.Contains("reference WSQ dimensions must be positive", error.Message);
    }

    [Theory]
    [InlineData(ExecutableType.BiomSharp)]
    [InlineData(ExecutableType.NativDswq)]
    public void Encoding_test_case_assertion_accepts_each_encoder(ExecutableType executable)
    {
        var testCase = CreateEncodingTestCase();
        testCase.AddWsqContainer(executable, new DataContainerWsq("encoded.wsq", CreateFixture()));

        new FixtureFullProcess().AssertTestCaseEncoding(testCase);
    }

    [Fact]
    public void Encoding_test_case_assertion_accepts_multiple_valid_results()
    {
        var testCase = CreateEncodingTestCase();
        testCase.AddWsqContainer(ExecutableType.BiomSharp, new DataContainerWsq("BiomSharp.wsq", CreateFixture()));
        testCase.AddWsqContainer(ExecutableType.NativDswq, new DataContainerWsq("NBIS.wsq", CreateFixture()));

        new FixtureFullProcess().AssertTestCaseEncoding(testCase);
    }

    [Fact]
    public void Encoding_test_case_assertion_checks_results_after_the_first()
    {
        var testCase = CreateEncodingTestCase();
        testCase.AddWsqContainer(ExecutableType.BiomSharp, new DataContainerWsq("BiomSharp.wsq", CreateFixture()));
        testCase.AddWsqContainer(ExecutableType.NativDswq, new DataContainerWsq("NBIS.wsq", CreateFixture(181)));

        var error = Assert.Throws<Xunit.Sdk.TrueException>(() =>
            new FixtureFullProcess().AssertTestCaseEncoding(testCase));

        Assert.Contains("NBIS.wsq", error.Message);
        Assert.Contains("coefficient indices differ", error.Message);
    }

    [Fact]
    public void Encoding_test_case_assertion_rejects_missing_test_case()
    {
        Assert.Throws<Xunit.Sdk.NotNullException>(() =>
            new FixtureFullProcess().AssertTestCaseEncoding(null!));
    }

    [Fact]
    public void Encoding_test_case_assertion_rejects_missing_reference()
    {
        var testCase = CreateEncodingTestCase() with { OriginalWsqData = null };
        testCase.AddWsqContainer(ExecutableType.BiomSharp, new DataContainerWsq("encoded.wsq", CreateFixture()));

        var error = Assert.Throws<Xunit.Sdk.TrueException>(() =>
            new FixtureFullProcess().AssertTestCaseEncoding(testCase));

        Assert.Contains("missing reference WSQ", error.Message);
        Assert.Contains(testCase.CaseId.ImageName, error.Message);
    }

    [Fact]
    public void Encoding_test_case_assertion_rejects_empty_results()
    {
        var testCase = CreateEncodingTestCase();

        var error = Assert.Throws<Xunit.Sdk.TrueException>(() =>
            new FixtureFullProcess().AssertTestCaseEncoding(testCase));

        Assert.Contains("missing encoding results", error.Message);
        Assert.Contains(testCase.CaseId.ImageName, error.Message);
    }

    [Fact]
    public void Encoding_test_case_assertion_rejects_null_result()
    {
        var testCase = CreateEncodingTestCase();
        testCase.EncodedResults[ExecutableType.BiomSharp] = null!;

        var error = Assert.Throws<Xunit.Sdk.TrueException>(() =>
            new FixtureFullProcess().AssertTestCaseEncoding(testCase));

        Assert.Contains("missing BiomSharp encoding result", error.Message);
    }

    private static TestCaseData CreateEncodingTestCase() =>
        TestCaseData.Create(
            new TestCaseIdentity("example.pgm", nameof(EncodingComparisonTests), string.Empty, "Encoding"),
            new WsqParameters { BitRate = 0.75f },
            new DataContainerPgm("Original", "example.pgm", SimpleBitmap.Gray8(new byte[1024], 32, 32)),
            new DataContainerWsq("reference.wsq", CreateFixture()));

    [Fact]
    public void Fixture_assertion_reports_coefficient_requirement_failures()
    {
        var reference = new DataContainerWsq("reference.wsq", CreateFixture());
        var actual = new DataContainerWsq("encoded.wsq", CreateFixture(181));
        var fixture = new FixtureFullProcess();

        var exception = Assert.Throws<Xunit.Sdk.TrueException>(() =>
            fixture.AssertEncoding(reference, actual));

        Assert.Contains("coefficient indices differ", exception.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Encoding_input_rejects_malformed_wsq_data(bool invalidActual)
    {
        byte[] valid = CreateFixture();
        byte[] invalid = valid.Concat(new byte[] { 0 }).ToArray();
        Assert.Throws<InvalidDataException>(() =>
        {
            var reference = new DataContainerWsq("reference.wsq", invalidActual ? valid : invalid);
            var encoded = new DataContainerWsq("encoded.wsq", invalidActual ? invalid : valid);
            new FixtureFullProcess().AssertEncoding(reference, encoded);
        });
    }

    [Theory]
    [InlineData(99600, true)]
    [InlineData(100400, true)]
    [InlineData(99599, false)]
    [InlineData(100401, false)]
    public void Size_tolerance_is_inclusive_and_reference_relative(long size, bool passes)
    {
        // Arrange.
        WsqInspection reference = Example();
        WsqInspection actual = reference with { SizeWithoutComments = size };

        // Act.
        EncodingTestComparison result = EncodingTestComparison.Compare(reference, actual); 

        // Assert.
        Assert.Equal(passes, result.Failures.Count == 0);
    }

    [Theory]
    [InlineData("99.949", true)]
    [InlineData("100.051", true)]
    [InlineData("99.948999", false)]
    [InlineData("100.051001", false)]
    public void Both_bin_width_tolerances_are_checked_without_rounding(string value, bool passes)
    {
        // Arrange.
        WsqInspection reference = Example();
        WsqInspection actual = Example();
        decimal width = decimal.Parse(value, CultureInfo.InvariantCulture);
        actual.BinWidths[59] = width;
        actual.ZeroWidths[59] = width;

        // Act.
        EncodingTestComparison result = EncodingTestComparison.Compare(reference, actual);

        // Assert.
        Assert.Equal(passes, result.Failures.Count == 0);
        Assert.Equal(passes ? 0 : 2, result.Bins.Count(b => !b.Passed));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Zero_reference_bins_require_exact_zero(int value, bool passes)
    {
        // Arrange.
        WsqInspection reference = Example();
        WsqInspection actual = Example();
        reference.BinWidths[0] = reference.ZeroWidths[0] = 0;
        actual.BinWidths[0] = actual.ZeroWidths[0] = value;

        // Act.
        EncodingTestComparison result = EncodingTestComparison.Compare(reference, actual);

        // Assert.
        Assert.Equal(passes, result.Failures.Count == 0);
        Assert.All(result.Bins.Where(b => b.Subband == 0), b => Assert.Null(b.DifferencePercent));
    }

    [Theory]
    [InlineData(10000, 1, 1, true)]
    [InlineData(10000, 2, 1, false)]
    [InlineData(9999, 1, 1, false)]
    [InlineData(19999, 1, 1, true)]
    [InlineData(19999, 2, 1, false)]
    [InlineData(20000, 2, 1, true)]
    [InlineData(10000, 1, 2, false)]
    [InlineData(10000, 1, -2, false)]
    public void Coefficient_identity_and_maximum_difference_are_separate_limits(
        int count, int changed, int difference, bool passes)
    {
        // Arrange.
        WsqInspection reference = Example(count);
        WsqInspection actual = Example(count);
        Array.Fill(actual.Coefficients, difference, 0, changed);

        // Act.
        EncodingTestComparison result = EncodingTestComparison.Compare(reference, actual);

        // Assert.
        Assert.Equal(passes, result.Failures.Count == 0);
        Assert.Equal(changed, result.Mismatches);
        Assert.Equal(count / 10000, result.AllowedMismatches);
        Assert.Equal(Math.Abs(difference), result.MaximumIndexDifference);
    }

    [Fact]
    public void Different_coefficient_counts_cannot_pass()
    {
        // Arrange.
        WsqInspection reference = Example();
        WsqInspection actual = Example(9999);

        // Act.
        EncodingTestComparison result = EncodingTestComparison.Compare(reference, actual);

        // Assert.
        Assert.Contains(result.Failures, f => f.Contains("counts differ"));
    }

    [Fact]
    public void Frame_dimensions_and_encoder_number_are_checked()
    {
        // Arrange.
        WsqInspection reference = Example();
        WsqInspection actual = reference with { Header = reference.Header with { Width = 1, Encoder = 0 } };

        // Act.
        EncodingTestComparison result = EncodingTestComparison.Compare(reference, actual);

        // Assert.
        Assert.Equal(2, result.Failures.Count);
    }

    [Theory]
    [InlineData(320, 240, true)]
    [InlineData(319, 240, false)]
    [InlineData(320, 239, false)]
    public void Comparison_uses_baseline_dimensions(int encodedWidth, int encodedHeight, bool passes)
    {
        var baseline = Example();
        baseline = baseline with { Header = baseline.Header with { Width = 320, Height = 240 } };
        var encoded = baseline with
        {
            Header = baseline.Header with { Width = encodedWidth, Height = encodedHeight }
        };

        var comparison = EncodingTestComparison.Compare(baseline, encoded);

        Assert.Equal(passes, comparison.Failures.Count == 0);
    }

    [Fact]
    public void Comparison_uses_first_argument_as_size_tolerance_baseline()
    {
        var baseline = Example() with { SizeWithoutComments = 100000 };
        var encoded = baseline with { SizeWithoutComments = 99600 };

        var comparison = EncodingTestComparison.Compare(baseline, encoded);

        Assert.Equal(0.4m, comparison.SizeDifferencePercent);
        Assert.Empty(comparison.Failures);
        Assert.NotEmpty(EncodingTestComparison.Compare(encoded, baseline).Failures);
    }

    [Theory]
    [InlineData(180, "", 0)]
    [InlineData(181, "", 1)]
    [InlineData(179, "", -1)]
    [InlineData(101, "11111111", 255)]
    [InlineData(102, "11111111", -255)]
    [InlineData(103, "0000000100000000", 256)]
    [InlineData(104, "0000000100000000", -256)]
    [InlineData(1, "", 0)]
    [InlineData(105, "00000001", 0)]
    [InlineData(106, "0000000000000001", 0)]
    public void Inspection_extracts_known_indices_without_reconstructing_pixels(int symbol, string extraBits, int expected)
    {
        // Arrange.
        byte[] fixture = CreateFixture((byte)symbol, extraBits);

        // Act.
        WsqInspection result = WsqInspection.Read(fixture);

        // Assert.
        Assert.Equal(new[] { expected }, result.Coefficients);
        Assert.Equal(new[] { 1, 0, 0 }, result.BlockCounts);
        Assert.Equal(1m, result.BinWidths[0]);
        Assert.Equal(1.2m, result.ZeroWidths[0]);
        Assert.Equal(0.44m, result.Center);
    }

    [Fact]
    public void Comment_segments_are_excluded_including_marker_and_length_bytes()
    {
        // Arrange.
        byte[] plain = CreateFixture();
        byte[] comment = { 0xff, 0xa8, 0, 5, 65, 66, 67 };
        byte[] commented = plain[..2].Concat(comment).Concat(plain[2..]).ToArray();
        WsqInspection reference = WsqInspection.Read(plain);

        // Act.
        WsqInspection actual = WsqInspection.Read(commented);

        // Assert.
        Assert.Equal(plain.Length, actual.SizeWithoutComments);
        Assert.Equal(reference.SizeWithoutComments, actual.SizeWithoutComments);
        Assert.Equal(reference.Coefficients, actual.Coefficients);
    }

    [Fact]
    public void Missing_coefficients_do_not_become_implicit_zeros()
    {
        // Arrange.
        byte[] fixture = CreateFixture(includeCoefficient: false);

        // Act.
        Exception? exception = Record.Exception(() => WsqInspection.Read(fixture));

        // Assert.
        Assert.IsType<InvalidDataException>(exception);
    }

    [Fact]
    public void Trailing_bytes_are_rejected()
    {
        // Arrange.
        byte[] fixture = CreateFixture().Concat(new byte[] { 0 }).ToArray();

        // Act.
        Exception? exception = Record.Exception(() => WsqInspection.Read(fixture));

        // Assert.
        Assert.IsType<InvalidDataException>(exception);
    }

    [Fact]
    public void Excess_coefficients_are_rejected()
    {
        // Arrange.
        byte[] fixture = CreateFixture(2);

        // Act.
        Exception? exception = Record.Exception(() => WsqInspection.Read(fixture));

        // Assert.
        Assert.IsType<InvalidDataException>(exception);
    }   
       

    private static WsqInspection Example(int coefficientCount = 10000) =>
        new WsqInspection(new WsqFrame(100, 100, 0, 255, 2, 0, 7, 9), 100000, 0.44m,
            Enumerable.Repeat(100m, 60).ToArray(), Enumerable.Repeat(100m, 60).ToArray(),
            new int[coefficientCount], new[] { coefficientCount, 0, 0 });

    /// <summary>
    /// Hand-built entropy fixture: a 32x32 frame with only subband zero active (one coefficient).
    /// Its artificial transform table is for parser tests, not certification or image reconstruction.
    /// </summary>
    private static byte[] CreateFixture(byte symbol = 180, string extraBits = "", bool includeCoefficient = true)
    {
        var bytes = new List<byte> { 0xff, 0xa0 };
        void Segment(byte marker, byte[] content)
        {
            int length = content.Length + 2;
            bytes.AddRange(new byte[] { 0xff, marker, (byte)(length >> 8), (byte)length });
            bytes.AddRange(content);
        }

        Segment(0xa4, new byte[] { 9, 7 }.Concat(new byte[54]).ToArray());
        var quantization = new List<byte> { 2, 0, 44, 0, 0, 1, 1, 0, 12 };
        quantization.AddRange(new byte[63 * 6]);

        Segment(0xa5, quantization.ToArray());
        Segment(0xa2, new byte[] { 0, 255, 0, 32, 0, 32, 0, 0, 0, 0, 0, 1, 2, 0, 0 });
        Segment(0xa6, new byte[] { 0, 1 }.Concat(new byte[15]).Append(symbol).ToArray());
        Segment(0xa3, new byte[] { 0 });

        if (includeCoefficient)
        {
            var bits = new StringBuilder("0" + extraBits);
            while (bits.Length % 8 != 0)
            {
                bits.Append('1');
            }
            for (int i = 0; i < bits.Length; i += 8)
            {
                byte value = Convert.ToByte(bits.ToString(i, 8), 2);
                bytes.Add(value);
                if (value == 0xff)
                {
                    bytes.Add(0);
                }
            }
        }

        Segment(0xa3, new byte[] { 0 });
        Segment(0xa3, new byte[] { 0 });
        bytes.AddRange(new byte[] { 0xff, 0xa1 });
        return bytes.ToArray();
    }
}
