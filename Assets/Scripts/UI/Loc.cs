/// <summary>
/// The two game languages. Chosen in the start menu; every visible text goes through T().
/// </summary>
public static class Loc
{
    public static bool English;

    /// <summary>Returns the German or the English text, depending on the chosen language.</summary>
    public static string T(string de, string en) => English ? en : de;
}
