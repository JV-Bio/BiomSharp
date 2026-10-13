using BiomSharp.Imaging.Tests.Fixtures;
using BiomSharp.Imaging.Tests.Models;
using BiomSharp.Imaging.Wsq;
using Xunit.Sdk;

namespace BiomSharp.Imaging.Tests.TestInfrastructureTests;

/// <summary>
/// Verifies fixture-level decoding orchestration. Numerical comparison boundaries
/// are covered by <see cref="DecodingComparisonTests"/>.
/// </summary>
public class FullProcessAssertionTests
{
    [Fact]
    public void Decoding_assertion_accepts_agreeing_decoders_despite_lossy_original_differences()
    {
        // Arrange
        var testCase = CreateDecodingTestCase();

        // Act
        var exception = Record.Exception(() => new FixtureFullProcess().AssertTestCaseDecoding(testCase));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Decoding_assertion_propagates_all_decoder_agreement_failures()
    {
        // Arrange
        var testCase = CreateDecodingTestCase();
        testCase.AddPgmContainer(ExecutableType.BiomSharp, CreatePgm("BiomSharp", 3, 2, 130));
        var expected = DecodingTestComparison.Compare(
            testCase.DecodedResults[ExecutableType.NativDswq],
            testCase.DecodedResults[ExecutableType.BiomSharp],
            DecodingComparisonMode.DecoderAcceptance);

        // Act
        var exception = Record.Exception(() =>
            new FixtureFullProcess().AssertTestCaseDecoding(testCase));

        // Assert
        Assert.Equal(2, expected.Failures.Count);
        var error = Assert.IsType<TrueException>(exception);
        foreach (string failure in expected.Failures)
            Assert.Contains(failure, error.Message);
    }

    [Theory]
    [InlineData(ExecutableType.BiomSharp)]
    [InlineData(ExecutableType.NativDswq)]
    public void Decoding_assertion_rejects_missing_expected_result(ExecutableType executable)
    {
        // Arrange
        var testCase = CreateDecodingTestCase();
        testCase.RemovePgmContainer(executable);

        // Act
        var exception = Record.Exception(() =>
            new FixtureFullProcess().AssertTestCaseDecoding(testCase));

        // Assert
        var error = Assert.IsType<TrueException>(exception);
        Assert.Contains(testCase.CaseId.ImageName, error.Message);
        Assert.Contains("missing", error.Message);
        Assert.Contains(executable == ExecutableType.BiomSharp ? "BiomSharp" : "NIST native dwsq", error.Message);
    }

    [Theory]
    [InlineData(ExecutableType.BiomSharp)]
    [InlineData(ExecutableType.NativDswq)]
    public void Decoding_assertion_rejects_null_result(ExecutableType executable)
    {
        // Arrange
        var testCase = CreateDecodingTestCase();
        testCase.DecodedResults[executable] = null!;

        // Act
        var exception = Record.Exception(() =>
            new FixtureFullProcess().AssertTestCaseDecoding(testCase));

        // Assert
        Assert.IsType<NotNullException>(exception);
    }

    [Theory]
    [InlineData(ExecutableType.BiomSharp)]
    [InlineData(ExecutableType.NativDswq)]
    public void Decoding_assertion_checks_each_result_against_original_dimensions(ExecutableType executable)
    {
        // Arrange
        var testCase = CreateDecodingTestCase();
        testCase.AddPgmContainer(executable, CreatePgm(executable.ToString(), 2, 3, 128));
        var expected = DecodingTestComparison.Compare(
            testCase.OriginalPgm, testCase.DecodedResults[executable], DecodingComparisonMode.MeasurementsOnly);

        // Act
        var exception = Record.Exception(() =>
            new FixtureFullProcess().AssertTestCaseDecoding(testCase));

        // Assert
        var error = Assert.IsType<TrueException>(exception);
        Assert.Contains(Assert.Single(expected.Failures), error.Message);
    }

    [Fact]
    public void Decoding_assertion_checks_original_dimensions_even_when_decoders_agree()
    {
        // Arrange
        var testCase = CreateDecodingTestCase() with { OriginalPgm = CreatePgm("Original", 2, 3) };

        // Act
        var exception = Record.Exception(() =>
            new FixtureFullProcess().AssertTestCaseDecoding(testCase));

        // Assert
        var error = Assert.IsType<TrueException>(exception);
        Assert.Contains("dimensions", error.Message);
    }

    [Fact]
    public void Decoding_assertion_rejects_missing_original_image()
    {
        // Arrange
        var testCase = CreateDecodingTestCase() with { OriginalPgm = null! };

        // Act
        var exception = Record.Exception(() =>
            new FixtureFullProcess().AssertTestCaseDecoding(testCase));

        // Assert
        var error = Assert.IsType<TrueException>(exception);
        Assert.Contains("Baseline reference PGM", error.Message);
    }

    [Fact]
    public void Decoding_assertion_rejects_missing_test_case()
    {
        // Arrange
        var fixture = new FixtureFullProcess();

        // Act
        var exception = Record.Exception(() => fixture.AssertTestCaseDecoding(null!));

        // Assert
        Assert.IsType<NotNullException>(exception);
    }

    [Fact]
    public void Decoding_assertion_rejects_empty_results()
    {
        // Arrange
        var testCase = CreateTestCase();

        // Act
        var exception = Record.Exception(() =>
            new FixtureFullProcess().AssertTestCaseDecoding(testCase));

        // Assert
        Assert.IsType<TrueException>(exception);
    }

    [Fact]
    public void Decoding_assertion_does_not_substitute_an_unrelated_result_for_a_required_decoder()
    {
        // Arrange
        var testCase = CreateDecodingTestCase();
        var native = testCase.DecodedResults[ExecutableType.NativDswq];
        testCase.RemovePgmContainer(ExecutableType.NativDswq);
        testCase.AddPgmContainer(ExecutableType.None, native);

        // Act
        var exception = Record.Exception(() =>
            new FixtureFullProcess().AssertTestCaseDecoding(testCase));

        // Assert
        var error = Assert.IsType<TrueException>(exception);
        Assert.Contains("missing 'NIST native dwsq'", error.Message);
    }

    private static TestCaseData CreateDecodingTestCase()
    {
        var testCase = CreateTestCase();
        testCase.AddPgmContainer(ExecutableType.NativDswq, CreatePgm("NIST", 3, 2, 128));
        testCase.AddPgmContainer(ExecutableType.BiomSharp, CreatePgm("BiomSharp", 3, 2, 128));
        return testCase;
    }

    private static TestCaseData CreateTestCase()
    {
        return TestCaseData.Create(
            new TestCaseIdentity("original.pgm", nameof(FullProcessAssertionTests), string.Empty, "Decoding"),
            new WsqParameters { BitRate = 0.75f }, CreatePgm("Original", 3, 2), null);
    }

    private static DataContainerPgm CreatePgm(string decoder, int width, int height, byte value = 0) =>
        new DataContainerPgm(decoder, "image.pgm",
            SimpleBitmap.Gray8(Enumerable.Repeat(value, width * height).ToArray(), width, height));
}
