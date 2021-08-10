using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AxonApiHelper
{
    public static class Extensions
    {
        public static T WaitForResult<T>(this Task<T> t)
        {
            t.Wait();
            return t.Result;
        }

        public static bool HasOverlap<T>(this List<T> list1, List<T> list2)
        {
            foreach (T t in list1)
            {
                if (list2.Contains(t))
                {
                    return true;
                }
            }
            return false;
        }

        public static List<List<T>> Split<T>(this List<T> list, int maxPer)
        {
            List<List<T>> superList = new();
            int offset = 0;
            while (offset < list.Count)
            {
                superList.Add(list.GetRange(offset, offset + maxPer > list.Count ? maxPer : list.Count % maxPer));
                offset += maxPer;
            }
            return superList;
        }

        public static double ToEpochTime(this DateTime dt)
        {
            return dt.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }
    }
}
