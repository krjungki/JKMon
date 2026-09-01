namespace JKMon.Core.Presentation;

/// <summary>How the CPU reading is drawn. Number shows the percentage alone, without a label.</summary>
public enum CpuGaugeStyle
{
    Number,
    Bar
}

public enum MemoryGaugeStyle
{
    Number,
    Bar,
    Pie
}

/// <summary>Where the caption sits over the metric row, which is usually wider than the caption itself.</summary>
public enum CaptionAlignment
{
    Left,
    Center,
    Right
}
