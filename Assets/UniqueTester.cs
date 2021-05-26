using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Object = System.Object;
using Random = System.Random;

public class UniqueTester : MonoBehaviour {
    private static readonly int[][] DEFAULT_COLORS = {
        new[] {0, 0, 1, 1, 2},
        new[] {3, 4, 4, 11, 2},
        new[] {3, 6, 7, 11, 9},
        new[] {5, 6, 7, 8, 9},
        new[] {5, 8, 8, 10, 10}
    };

    private static readonly int[][] DEFAULT_SYMBOLS = {
        new[] {70, 80, 75, 117, 93},
        new[] {10, 11, 12, 111, 14},
        new[] {0, 112, 2, 105, 20},
        new[] {62, 16, 103, 18, 19},
        new[] {4, 21, 22, 23, 24}
    };

    private static int[][] _best;
    private static int _bestCount = 0;
    private const int MAX = 8;
    private static readonly object LOCK_OBJ = new object();

    private static void Main(string[] args) {
        int threads, dummy;
        ThreadPool.GetMaxThreads(out threads, out dummy);

        for (int i = 0; i < threads; i++) {
            ThreadPool.QueueUserWorkItem(CheckLoop);
        }
    }

    private static void CheckLoop(object stateInfo) {
        var rand = new Random();
        while (true) {
            // Generate random array
            var arr = new int[5][];
            for (var i = 0; i < 5; i++) {
                arr[i] = new[] {
                    rand.Next(0, MAX), rand.Next(0, MAX), rand.Next(0, MAX), rand.Next(0, MAX),
                    rand.Next(0, MAX)
                };
            }

            // Check it
            var count = Check(arr);

            // Record best
            lock (LOCK_OBJ) {
                if (count.First <= _bestCount) continue;

                _best = arr;
                _bestCount = count.First;
                Debug.LogFormat("Found {0}/{1}", count.First, count.Second);
                //
                // if (count.First == count.Second) {
                //     Application.Quit();
                // }
            }
        }
    }

    private void OnDestroy() {
        foreach (var row in _best) {
            Debug.LogFormat(row.Join(", "));
        }
    }

    private static Pair<int, int> Check(int[][] colors) {
        var cubes = new HashSet<string>();
        var pos = 0;

        for (var netIndex = 0; netIndex < Cube.NET_STRINGS.Length; netIndex++) {
            // Choose cube net
            var net = Cube.NET_STRINGS[netIndex];
            foreach (var flipped in new[] {true, false}) {
                for (var rotation = 0; rotation < (netIndex == 0 ? 2 : 4); rotation++) {
                    var fixedNet = (flipped ? net.MirrorVertical() : net).Rotate(rotation);

                    // Choose net location
                    var splitNet = fixedNet.Split('\n');
                    for (var netPosX = 0; netPosX < 5 - splitNet[0].Length + 1; netPosX++) {
                        for (var netPosY = 0; netPosY < 5 - splitNet.Length + 1; netPosY++) {
                            var netPos = new Vector2Int(netPosX, netPosY);

                            // Find and log net colors
                            var netColors =
                                splitNet.Select((r, row) =>
                                        r.Select((val, col) =>
                                                val == ' ' ? -2 : colors[row + netPos.y][col + netPos.x])
                                            .ToArray())
                                    .ToArray();

                            var netSymbols =
                                splitNet.Select((r, row) =>
                                        r.Select((val, col) =>
                                                val == ' ' ? -2 : DEFAULT_SYMBOLS[row + netPos.y][col + netPos.x])
                                            .ToArray())
                                    .ToArray();

                            // Create cube
                            var cube = Cube.MakeCube(netIndex,
                                flipped
                                    ? netColors.Rotate(-rotation + 4).Select(x => x.ToList()).MirrorVertical().ToList()
                                    : netColors.Rotate(-rotation + 4).Select(x => x.ToList()).ToList(),
                                flipped
                                    ? netSymbols.Rotate(-rotation + 4).Select(x => x.ToList()).MirrorVertical().ToList()
                                    : netSymbols.Rotate(-rotation + 4).Select(x => x.ToList()).ToList(),
                                rotation,
                                flipped
                            );

                            ++pos;
                            GenCubeOrientations(cubes, cube);
                        }
                    }
                }
            }
        }

        // Debug.LogFormat("{0} {1} {2} {3}", pos, pos * 24, cubes.Count, cubes.Count / 24);
        return new Pair<int, int>(cubes.Count, pos * 24);
    }

    private static void GenCubeOrientations(HashSet<string> cubes, Cube cube) {
        for (var i = 0; i < 4; i++) {
            for (var j = 0; j < 4; j++) {
                cube.RotateRight();
                AddCubeString(cubes, cube);
            }

            cube.RotateUp();
        }

        cube.RotateRight();
        cube.RotateUp();
        for (var i = 0; i < 4; i++) {
            AddCubeString(cubes, cube);
            cube.RotateRight();
        }

        cube.RotateDown();
        cube.RotateDown();
        for (var i = 0; i < 4; i++) {
            AddCubeString(cubes, cube);
            cube.RotateRight();
        }
    }

    private static void AddCubeString(HashSet<string> cubes, Cube cube) {
        var res = "";
        for (var i = 0; i < 6; i++) {
            res += (char) ('A' + cube.GetColor((CubeFace) i));
        }

        cubes.Add(res);
    }
}