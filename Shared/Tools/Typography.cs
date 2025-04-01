namespace Shared.Tools;

public static class Typography
{
    public static string ToUpperFirstLetter(string input)
    {
        return $"{char.ToUpper(input[0])}{input[1..]}";
    }
}