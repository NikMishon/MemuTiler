using System;
using System.Collections.Generic;

namespace MemuTiler
{
    public class StringNumberComparer : IComparer<string>
    {
        private static StringNumberComparer _instance;
        public static StringNumberComparer Instance => _instance ?? (_instance = new StringNumberComparer());

        public int Compare(string x, string y)
        {
            if (!int.TryParse(x, out int intX) || !int.TryParse(y, out int intY))
                return string.Compare(x, y, StringComparison.Ordinal);

            return intX.CompareTo(intY);
        }
    }
}

