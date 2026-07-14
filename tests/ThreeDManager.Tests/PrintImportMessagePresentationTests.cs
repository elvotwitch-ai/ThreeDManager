using ThreeDManager.Domain.Entities;
using ThreeDManager.Web.Presentation;

namespace ThreeDManager.Tests;

public class PrintImportMessagePresentationTests
{
    [Fact]
    public void IsWarning_WhenParsedImportCarriesAMessage_IsTrue()
    {
        // Process() stores parser warnings in ErrorMessage of a successfully parsed import.
        Assert.True(PrintImportMessagePresentation.IsWarning(
            PrintImportStatus.Parsed,
            "Tempo estimado não encontrado no arquivo."));
    }

    [Fact]
    public void IsWarning_WhenImportFailed_IsFalse()
    {
        Assert.False(PrintImportMessagePresentation.IsWarning(
            PrintImportStatus.Error,
            "Nenhum parser disponível para este arquivo."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsWarning_WhenNoMessage_IsFalse(string? errorMessage)
    {
        Assert.False(PrintImportMessagePresentation.IsWarning(PrintImportStatus.Parsed, errorMessage));
    }

    [Fact]
    public void IsFailure_WhenImportFailed_IsTrue()
    {
        Assert.True(PrintImportMessagePresentation.IsFailure(
            PrintImportStatus.Error,
            "Nenhum parser disponível para este arquivo."));
    }

    [Fact]
    public void IsFailure_WhenParsedImportCarriesAMessage_IsFalse()
    {
        Assert.False(PrintImportMessagePresentation.IsFailure(
            PrintImportStatus.Parsed,
            "Tempo estimado não encontrado no arquivo."));
    }

    [Fact]
    public void IsFailure_ForAnUnexpectedStatus_StillSurfacesTheMessage()
    {
        // A message must never be hidden just because the status is not one of the known constants.
        Assert.True(PrintImportMessagePresentation.IsFailure("Something", "algo aconteceu"));
        Assert.True(PrintImportMessagePresentation.IsFailure(PrintImportStatus.Uploaded, "algo aconteceu"));
    }

    [Fact]
    public void WarningAndFailure_AreMutuallyExclusive()
    {
        const string message = "Tempo estimado não encontrado no arquivo.";

        Assert.NotEqual(
            PrintImportMessagePresentation.IsWarning(PrintImportStatus.Parsed, message),
            PrintImportMessagePresentation.IsFailure(PrintImportStatus.Parsed, message));
    }

    [Fact]
    public void SplitMessages_SplitsOnTheSeparatorProcessUsesToJoinWarnings()
    {
        var warnings = new[]
        {
            "Tempo estimado não encontrado no arquivo.",
            "Tipo de material não encontrado no arquivo."
        };

        var joined = string.Join(PrintImportMessagePresentation.MessageSeparator, warnings);

        Assert.Equal(warnings, PrintImportMessagePresentation.SplitMessages(joined));
    }

    [Fact]
    public void SplitMessages_WithoutASeparator_ReturnsTheSingleMessage()
    {
        Assert.Equal(
            new[] { "Nenhum parser disponível para este arquivo." },
            PrintImportMessagePresentation.SplitMessages("Nenhum parser disponível para este arquivo."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SplitMessages_WhenNoMessage_IsEmpty(string? errorMessage)
    {
        Assert.Empty(PrintImportMessagePresentation.SplitMessages(errorMessage));
    }
}
