namespace Focus;

public enum RenderingEngine
{
    Chromium,
    Firefox,
    Webkit
}

public enum ResponseTimeRange
{
    LessThan450Ms,
    MoreThan450MsLessThan900Ms,
    MoreThan900Ms
}