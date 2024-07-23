using System;
using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
    public static string Filter(this string str, List<char> charsToRemove)
    {
        foreach (char c in charsToRemove)
        {
            str = str.Replace(c.ToString(), String.Empty);
        }

        return str;
    }

    public static string RemoveDigits(this string str)
    {
        return new string(str.ToCharArray().Where(n => !char.IsDigit(n)).ToArray());
    }

    public static string RemoveNij(this string str)
    {
        return str.Replace("_", "");
    }

    public static int RemoveLetters(this string str)
    {
        if (int.TryParse(new string(str.ToCharArray().Where(n => char.IsDigit(n)).ToArray()), out int num))
            return Int32.Parse(new string(str.ToCharArray().Where(n => char.IsDigit(n)).ToArray()));
        else
            return 0;
    }
}