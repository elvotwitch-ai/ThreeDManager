using System.Globalization;
using System.Text.RegularExpressions;
using ThreeDManager.Application.DTOs;
using ThreeDManager.Application.Interfaces;

namespace ThreeDManager.Infrastructure.Parsers;

public class GCodePrintFileParser : IPrintFileParser
{
    public bool CanParse(string fileName, string? rawContent)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension is ".gcode" or ".g")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return false;
        }

        return rawContent.Contains("G1", StringComparison.OrdinalIgnoreCase)
            || rawContent.Contains("G28", StringComparison.OrdinalIgnoreCase)
            || rawContent.Contains(";TIME", StringComparison.OrdinalIgnoreCase);
    }

    public ParsedPrintMetadata Parse(string fileName, string rawContent)
    {
        var metadata = new ParsedPrintMetadata
        {
            SlicerName = ExtractString(rawContent,
                @"(?im)^\s*;\s*generated\s+(?:by|with)\s*(?<value>[^\r\n]+)",
                @"(?im)^\s*;\s*(?<value>Creality\s+Print[^\r\n]*)")
        };

        metadata.EstimatedTimeMinutes = ExtractEstimatedTimeMinutes(rawContent);

        metadata.FilamentUsedGrams = ExtractDecimal(rawContent,
            @"(?im)^\s*;\s*filament.*(?:used|weight|mass).*\[?g\]?.*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)",
            @"(?im)^\s*;\s*filament\s+used\s*\(g\)\s*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)",
            @"(?im)^\s*;\s*filament\s+used\s*\[g\]\s*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)");

        metadata.FilamentUsedMeters = ExtractDecimal(rawContent,
            @"(?im)^\s*;\s*filament.*(?:used|length).*\[?m\]?.*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)",
            @"(?im)^\s*;\s*filament\s+used\s*\(m\)\s*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)",
            @"(?im)^\s*;\s*filament\s+used\s*\[m\]\s*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)");

        metadata.MaterialType = ExtractString(rawContent,
            @"(?im)^\s*;\s*material\s*[=:]\s*(?<value>[^\r\n;]+)",
            @"(?im)^\s*;\s*filament\s+type\s*[=:]\s*(?<value>[^\r\n;]+)",
            @"(?im)^\s*;\s*filament_type\s*[=:]\s*(?<value>[^\r\n;]+)");

        metadata.NozzleTemperature = ExtractDecimal(rawContent,
            @"(?im)^\s*;\s*nozzle.*temperature\s*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)",
            @"(?im)^\s*M104\s+.*S(?<value>[-+]?\d+(?:[.,]\d+)?)",
            @"(?im)^\s*M109\s+.*S(?<value>[-+]?\d+(?:[.,]\d+)?)");

        metadata.BedTemperature = ExtractDecimal(rawContent,
            @"(?im)^\s*;\s*bed.*temperature\s*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)",
            @"(?im)^\s*M140\s+.*S(?<value>[-+]?\d+(?:[.,]\d+)?)",
            @"(?im)^\s*M190\s+.*S(?<value>[-+]?\d+(?:[.,]\d+)?)");

        metadata.LayerHeight = ExtractDecimal(rawContent,
            @"(?im)^\s*;\s*layer\s*height\s*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)",
            @"(?im)^\s*;\s*layer_height\s*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)");

        metadata.InfillPercentage = ExtractDecimal(rawContent,
            @"(?im)^\s*;\s*infill.*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)\s*%",
            @"(?im)^\s*;\s*sparse_infill_density\s*[=:]\s*(?<value>[-+]?\d+(?:[.,]\d+)?)\s*%");

        metadata.ReportedCost = ExtractDecimal(rawContent,
            @"(?im)^\s*;\s*(?:cost|total cost|filament cost|print cost)\s*[=:]\s*(?:R\$|\$)?\s*(?<value>[-+]?\d+(?:[.,]\d+)?)");

        AddWarnings(metadata);

        return metadata;
    }

    private static string? ExtractString(string rawContent, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(rawContent, pattern);

            if (match.Success)
            {
                var value = match.Groups["value"].Value.Trim();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static decimal? ExtractDecimal(string rawContent, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(rawContent, pattern);

            if (!match.Success)
            {
                continue;
            }

            var value = match.Groups["value"].Value.Trim();

            if (TryParseDecimal(value, out var number))
            {
                return number;
            }
        }

        return null;
    }

    private static int? ExtractEstimatedTimeMinutes(string rawContent)
    {
        var timeInSeconds = ExtractDecimal(rawContent,
            @"(?im)^\s*;\s*TIME\s*:\s*(?<value>\d+)");

        if (timeInSeconds.HasValue)
        {
            return (int)Math.Ceiling((double)timeInSeconds.Value / 60.0);
        }

        var timeText = ExtractString(rawContent,
            @"(?im)^\s*;\s*estimated.*(?:print|printing).*time.*[=:]\s*(?<value>[^\r\n]+)",
            @"(?im)^\s*;\s*print.*time.*[=:]\s*(?<value>[^\r\n]+)");

        if (string.IsNullOrWhiteSpace(timeText))
        {
            return null;
        }

        return ParseHumanDurationToMinutes(timeText);
    }

    private static int? ParseHumanDurationToMinutes(string value)
    {
        value = value.Trim().ToLowerInvariant();

        var colonMatch = Regex.Match(value, @"^(?<a>\d+):(?<b>\d{1,2})(?::(?<c>\d{1,2}))?$");

        if (colonMatch.Success)
        {
            var a = int.Parse(colonMatch.Groups["a"].Value, CultureInfo.InvariantCulture);
            var b = int.Parse(colonMatch.Groups["b"].Value, CultureInfo.InvariantCulture);
            var hasSeconds = colonMatch.Groups["c"].Success;

            if (hasSeconds)
            {
                var c = int.Parse(colonMatch.Groups["c"].Value, CultureInfo.InvariantCulture);
                return (int)Math.Ceiling(a * 60 + b + c / 60.0);
            }

            return (int)Math.Ceiling((double)(a * 60 + b));
        }

        var hours = ExtractDurationPart(value, @"(?<value>\d+(?:[.,]\d+)?)\s*(?:h|hour|hours|hora|horas)");
        var minutes = ExtractDurationPart(value, @"(?<value>\d+(?:[.,]\d+)?)\s*(?:m|min|minute|minutes|minuto|minutos)");
        var seconds = ExtractDurationPart(value, @"(?<value>\d+(?:[.,]\d+)?)\s*(?:s|sec|second|seconds|segundo|segundos)");

        if (hours is null && minutes is null && seconds is null)
        {
            return null;
        }

        var totalMinutes = 0m;

        if (hours.HasValue)
        {
            totalMinutes += hours.Value * 60;
        }

        if (minutes.HasValue)
        {
            totalMinutes += minutes.Value;
        }

        if (seconds.HasValue)
        {
            totalMinutes += seconds.Value / 60;
        }

        return (int)Math.Ceiling((double)totalMinutes);
    }

    private static decimal? ExtractDurationPart(string value, string pattern)
    {
        var match = Regex.Match(value, pattern);

        if (!match.Success)
        {
            return null;
        }

        return TryParseDecimal(match.Groups["value"].Value, out var number)
            ? number
            : null;
    }

    private static bool TryParseDecimal(string value, out decimal number)
    {
        value = value
            .Replace("R$", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .Replace(',', '.');

        return decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static void AddWarnings(ParsedPrintMetadata metadata)
    {
        if (metadata.EstimatedTimeMinutes is null)
        {
            metadata.Warnings.Add("Tempo estimado não encontrado no arquivo.");
        }

        if (metadata.FilamentUsedGrams is null && metadata.FilamentUsedMeters is null)
        {
            metadata.Warnings.Add("Consumo de filamento não encontrado no arquivo.");
        }

        if (metadata.MaterialType is null)
        {
            metadata.Warnings.Add("Tipo de material não encontrado no arquivo.");
        }

        if (metadata.SlicerName is null)
        {
            metadata.Warnings.Add("Slicer/gerador do arquivo não identificado.");
        }
    }
}