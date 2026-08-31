namespace ZoneManager.Core.Models;

public sealed record EdgeInsets(int Left, int Top, int Right, int Bottom)
{
    public static EdgeInsets Uniform(int value) => new(value, value, value, value);

    public EdgeInsets Clamp(int minimum, int maximum) => new(
        Math.Clamp(Left, minimum, maximum),
        Math.Clamp(Top, minimum, maximum),
        Math.Clamp(Right, minimum, maximum),
        Math.Clamp(Bottom, minimum, maximum));
}

