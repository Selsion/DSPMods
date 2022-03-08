using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    static class Utils
    {
        private static Random rng = new Random();

        public static void Shuffle<T>(this IList<T> list, int n = -1)
        {
            if (n == -1)
                n = list.Count;
            while (n > 1)
                list.RandomPrefixSwap(--n);
        }

        public static void RandomPrefixSwap<T>(this IList<T> list, int idx)
        {
            int k = rng.Next(idx + 1);
            T value = list[k];
            list[k] = list[idx];
            list[idx] = value;
        }

        // should be equivalent to std::lower_bound in c++
        // where does C# provide this functionality? Array.BinarySearch doesn't give the first match
        public static int LowerBound<T>(IList<T> list, T val)
            where T : IComparable<T>
        {
            return LowerBound(list, val, 0, list.Count);
        }

        // should be equivalent to std::lower_bound in c++
        // where does C# provide this functionality? Array.BinarySearch doesn't give the first match
        /*public static int LowerBound<T>(IList<T> list, T val, int low, int high)
            where T : IComparable<T>
        {
            while (low <= high)
            {
                int mid = (low + high) / 2;

                if (list[mid].CompareTo(val) < 0) // list[mid] < val
                    low = mid + 1;
                else if (list[mid].CompareTo(val) > 0) // list[mid] > val
                    high = mid - 1;
                else if (low != mid)
                    high = mid - 1;
                else
                    return mid;
            }

            return low;
        }*/

        // should be equivalent to std::lower_bound in c++
        // where does C# provide this functionality? Array.BinarySearch doesn't give the first match
        public static int LowerBound<T>(IList<T> list, T val, int low, int high)
            where T : IComparable<T>
        {
            while (low < high)
            {
                int mid = low + (high - low) / 2;

                if (val.CompareTo(list[mid]) <= 0)
                    high = mid;
                else
                    low = mid + 1;
            }

            return low;
        }
    }
}
