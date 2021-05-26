using System;
using System.Collections.Generic;
using System.Linq;

public static class Extensions {
    public static List<List<T>> ChunkBy<T>(this List<T> source, int chunkSize) {
        return source
            .Select((x, i) => new {Index = i, Value = x})
            .GroupBy(x => x.Index / chunkSize)
            .Select(x => x.Select(v => v.Value).ToList())
            .ToList();
    }

    public delegate void BiAction<T1, T2>(T1 first, T2 second);

    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items, int maxItems) {
        return items.Select((item, inx) => new {item, inx}).GroupBy(x => x.inx / maxItems)
            .Select(g => g.Select(x => x.item));
    }

    public static IEnumerable<T> Peek<T>(this IEnumerable<T> items, Action<T> action) {
        var enumerable = items.ToList();
        foreach (var item in enumerable) {
            action.Invoke(item);
        }

        return enumerable;
    }

    public static IEnumerable<T> Peek<T>(this IEnumerable<T> items, BiAction<T, int> action) {
        var enumerable = items.ToList();
        for (var index = 0; index < enumerable.Count; index++) {
            var item = enumerable[index];
            action.Invoke(item, index);
        }

        return enumerable;
    }

    public static string Reverse(this string str) {
        if (str == null) return null;

        var array = str.ToCharArray();
        Array.Reverse(array);
        return new string(array);
    }

    public static string MirrorVertical(this string str) {
        return str.Split('\n').Select(s => s.Reverse()).Join("\n");
    }
    
    public static IEnumerable<List<T>> MirrorVertical<T>(this IEnumerable<List<T>> arr) {
        return arr.Select(s => {
            var l = new List<T>(s);
            l.Reverse();
            return l;
        });
    }
    
    public static IEnumerable<List<T>> MirrorHorizontal<T>(this IEnumerable<List<T>> arr) {
        return arr.Reverse();
    }

    public static string Rotate(this string str, int count) {
        count %= 4;
        while (true) {
            if (count < 0 || str.Length < 1) {
                throw new ArgumentException();
            }

            if (count == 0) {
                return str;
            }

            // Create arrays
            var arr = str.Split('\n').Select(s => s.ToCharArray()).ToArray();
            var resultArr = new char[arr[0].Length][];
            for (var i = 0; i < arr[0].Length; i++) {
                resultArr[i] = new char[arr.Length];
            }

            for (var i = 0; i < arr[0].Length; ++i) {
                for (var j = 0; j < arr.Length; ++j) {
                    resultArr[i][j] = arr[arr.Length - j - 1][i];
                }
            }

            // Create result
            var result = resultArr.Select(ca => new string(ca)).Join("\n");

            str = result;
            --count;
        }
    }
    
    public static T[][] Rotate<T>(this T[][] arr, int count) {
        count %= 4;
        while (true) {
            if (count < 0 || arr.Length < 1) {
                throw new ArgumentException();
            }

            if (count == 0) {
                return arr;
            }

            // Create arrays
            var resultArr = new T[arr[0].Length][];
            for (var i = 0; i < arr[0].Length; i++) {
                resultArr[i] = new T[arr.Length];
            }

            for (var i = 0; i < arr[0].Length; ++i) {
                for (var j = 0; j < arr.Length; ++j) {
                    resultArr[i][j] = arr[arr.Length - j - 1][i];
                }
            }

            arr = resultArr;
            --count;
        }
    }
}