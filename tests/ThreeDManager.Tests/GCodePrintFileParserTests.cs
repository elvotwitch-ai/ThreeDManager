using ThreeDManager.Infrastructure.Parsers;

namespace ThreeDManager.Tests;

public class GCodePrintFileParserTests
{
    private static readonly GCodePrintFileParser Parser = new();

    private static string Footer(params string[] lines) => string.Join("\n", lines);

    // --- Filament length: the unit must be read, not assumed ---------------------------------

    [Fact]
    public void FilamentUsedMeters_ConvertsMillimetres_ForPrusaSlicerFooter()
    {
        // PrusaSlicer/OrcaSlicer state the length in millimetres. 1234.5 mm is 1.2345 m,
        // not 1234.5 m.
        var metadata = Parser.Parse("cube.gcode", "; filament used [mm] = 1234.5");

        Assert.Equal(1.2345m, metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_ConvertsCentimetres()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament used [cm] = 123.45");

        Assert.Equal(1.2345m, metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_ReadsMetresUnchanged()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament used [m] = 1.2345");

        Assert.Equal(1.2345m, metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_ReadsLegacyParenthesisedMetres()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament used (m) = 1.2345");

        Assert.Equal(1.2345m, metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_ReadsFilamentLengthLabel()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament length [m] = 1.2345");

        Assert.Equal(1.2345m, metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_IgnoresVolume_BecauseCubicCentimetresAreNotALength()
    {
        // "[cm3]" is a volume. Reading it as a length reports 2.97 m of filament for a
        // 2.97 cm3 extrusion.
        var metadata = Parser.Parse("cube.gcode", "; filament used [cm3] = 2.97");

        Assert.Null(metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_IgnoresCubicMillimetreVolume()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament used [mm3] = 2970.0");

        Assert.Null(metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_PrefersTheLengthLine_OverTheVolumeLineThatPrecedesIt()
    {
        // Real PrusaSlicer footers list mm, then cm3, then g. The volume line must not win.
        var metadata = Parser.Parse("cube.gcode", Footer(
            "; filament used [mm] = 1234.5",
            "; filament used [cm3] = 2.97",
            "; filament used [g] = 3.68"));

        Assert.Equal(1.2345m, metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_ReadsCuraSuffixNotation()
    {
        // Cura writes the unit as a suffix on the value rather than a bracketed label.
        var metadata = Parser.Parse("cube.gcode", ";Filament used: 1.2345m");

        Assert.Equal(1.2345m, metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_SumsCuraPerExtruderLengths()
    {
        // A multi-extruder Cura footer lists one length per extruder; the print consumed both.
        var metadata = Parser.Parse("cube.gcode", ";Filament used: 1.2345m, 0.5m");

        Assert.Equal(1.7345m, metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_IsNull_WhenNoUnitIsStated()
    {
        // Without a unit the number is unusable; guessing metres is what caused the defect.
        var metadata = Parser.Parse("cube.gcode", "; filament used = 1234.5");

        Assert.Null(metadata.FilamentUsedMeters);
    }

    [Fact]
    public void FilamentUsedMeters_IsNull_WhenAbsent()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament used [g] = 3.68");

        Assert.Null(metadata.FilamentUsedMeters);
    }

    // --- Filament weight: guard the behaviour the length fix must not disturb ------------------

    [Fact]
    public void FilamentUsedGrams_ReadsBracketedGrams()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament used [g] = 3.68");

        Assert.Equal(3.68m, metadata.FilamentUsedGrams);
    }

    [Fact]
    public void FilamentUsedGrams_ReadsLegacyParenthesisedGrams()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament used (g) = 3.68");

        Assert.Equal(3.68m, metadata.FilamentUsedGrams);
    }

    [Fact]
    public void FilamentUsedGrams_ReadsMassLabel()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament mass [g] = 3.68");

        Assert.Equal(3.68m, metadata.FilamentUsedGrams);
    }

    [Fact]
    public void FilamentUsedGrams_IsNotFooledByALengthOnlyFooter()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament used [mm] = 1234.5");

        Assert.Null(metadata.FilamentUsedGrams);
    }

    [Fact]
    public void FilamentUsedGrams_AndMeters_AreBothReadFromAFullFooter()
    {
        var metadata = Parser.Parse("cube.gcode", Footer(
            "; filament used [mm] = 1234.5",
            "; filament used [cm3] = 2.97",
            "; filament used [g] = 3.68"));

        Assert.Equal(3.68m, metadata.FilamentUsedGrams);
        Assert.Equal(1.2345m, metadata.FilamentUsedMeters);
    }

    // --- Warnings ----------------------------------------------------------------------------

    [Fact]
    public void Warnings_DoNotReportMissingFilament_WhenOnlyALengthIsStated()
    {
        // A Cura footer states length but no weight; consumption *was* found.
        var metadata = Parser.Parse("cube.gcode", ";Filament used: 1.2345m");

        Assert.DoesNotContain(metadata.Warnings, warning => warning.Contains("Consumo de filamento"));
    }

    [Fact]
    public void Warnings_ReportMissingFilament_WhenOnlyAVolumeIsStated()
    {
        // A volume is neither a length nor a weight, so consumption is genuinely unknown.
        var metadata = Parser.Parse("cube.gcode", "; filament used [cm3] = 2.97");

        Assert.Contains(metadata.Warnings, warning => warning.Contains("Consumo de filamento"));
    }

    // --- Slicer name: the per-file build timestamp must not become part of the name ------------

    [Fact]
    public void SlicerName_StripsThePrusaSlicerBuildTimestamp()
    {
        // Left in, "on 2026-07-15 at 10:00:00" makes every export of the same slicer a distinct
        // name, so imports cannot be grouped by slicer.
        var metadata = Parser.Parse("cube.gcode",
            "; generated by PrusaSlicer 2.7.1 on 2026-07-15 at 10:00:00");

        Assert.Equal("PrusaSlicer 2.7.1", metadata.SlicerName);
    }

    [Fact]
    public void SlicerName_KeepsTheVersionButDropsTheTimestampAndTimezone()
    {
        var metadata = Parser.Parse("cube.gcode",
            "; generated by PrusaSlicer 2.7.1+win64 on 2024-01-15 at 10:23:45 UTC");

        Assert.Equal("PrusaSlicer 2.7.1+win64", metadata.SlicerName);
    }

    [Fact]
    public void SlicerName_IsStable_AcrossTwoExportsOfTheSameSlicer()
    {
        // The whole point: two files from the same slicer at different times must group together.
        var morning = Parser.Parse("a.gcode",
            "; generated by OrcaSlicer 1.9.0 on 2026-07-15 at 08:00:00").SlicerName;
        var evening = Parser.Parse("b.gcode",
            "; generated by OrcaSlicer 1.9.0 on 2026-07-15 at 20:30:11").SlicerName;

        Assert.Equal("OrcaSlicer 1.9.0", morning);
        Assert.Equal(morning, evening);
    }

    [Fact]
    public void SlicerName_LeavesCuraUntouched_BecauseItHasNoTimestampSuffix()
    {
        var metadata = Parser.Parse("cube.gcode", ";Generated with Cura_SteamEngine 5.4.0");

        Assert.Equal("Cura_SteamEngine 5.4.0", metadata.SlicerName);
    }

    [Fact]
    public void SlicerName_LeavesTheCrealityLineUntouched()
    {
        var metadata = Parser.Parse("cube.gcode", "; Creality Print 5.0");

        Assert.Equal("Creality Print 5.0", metadata.SlicerName);
    }

    [Fact]
    public void SlicerName_IsNull_WhenNoGeneratorLineIsPresent()
    {
        var metadata = Parser.Parse("cube.gcode", "; filament used [g] = 3.68");

        Assert.Null(metadata.SlicerName);
        Assert.Contains(metadata.Warnings, warning => warning.Contains("Slicer"));
    }

    // --- Estimated time (regression guard around the shared decimal helper) --------------------

    [Fact]
    public void EstimatedTimeMinutes_ReadsCuraSecondsHeader()
    {
        var metadata = Parser.Parse("cube.gcode", ";TIME:3600");

        Assert.Equal(60, metadata.EstimatedTimeMinutes);
    }

    [Fact]
    public void EstimatedTimeMinutes_ReadsHumanDuration()
    {
        var metadata = Parser.Parse("cube.gcode", "; estimated printing time (normal mode) = 1h 23m 45s");

        Assert.Equal(84, metadata.EstimatedTimeMinutes);
    }
}
