using ThreeDManager.Application.DTOs;

namespace ThreeDManager.Application.Interfaces;

public interface IPrintFileParser
{
    bool CanParse(string fileName, string? rawContent);

    ParsedPrintMetadata Parse(string fileName, string rawContent);
}