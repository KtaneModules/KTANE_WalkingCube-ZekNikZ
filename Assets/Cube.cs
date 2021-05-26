using System;
using System.Collections.Generic;
using System.Linq;

public enum CubeRotation {
    RIGHT,
    LEFT,
    UP,
    DOWN
}

public enum CubeFace {
    UP = 0,
    DOWN = 1,
    BACK = 2,
    RIGHT = 3,
    FRONT = 4,
    LEFT = 5
}

public class Cube {
    public static readonly string[] NET_STRINGS = {
        " X\n X\nXX\nX \nX ",
        "X  \nXXX\n X \n X ",
        "X  \nXX \n XX\n X "
    };

    private int[] _colors;
    private int[] _symbols;
    private int[] _rotations;

    private Cube(int[] colors, int[] symbols, int[] rotations) {
        _colors = colors;
        _symbols = symbols;
        _rotations = rotations;
    }

    public int GetColor(CubeFace face) {
        return _colors[(int) face];
    }

    public int GetSymbol(CubeFace face) {
        return _symbols[(int) face];
    }

    public int GetRotation(CubeFace face) {
        return _rotations[(int) face];
    }

    public void Rotate(CubeRotation rot) {
        switch (rot) {
            case CubeRotation.RIGHT:
                RotateRight();
                break;
            case CubeRotation.LEFT:
                RotateLeft();
                break;
            case CubeRotation.UP:
                RotateUp();
                break;
            case CubeRotation.DOWN:
                RotateDown();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void RotateUp() {
        _colors = new[] {
            _colors[(int) CubeFace.BACK],
            _colors[(int) CubeFace.FRONT],
            _colors[(int) CubeFace.DOWN],
            _colors[(int) CubeFace.RIGHT],
            _colors[(int) CubeFace.UP],
            _colors[(int) CubeFace.LEFT],
        };
        _symbols = new[] {
            _symbols[(int) CubeFace.BACK],
            _symbols[(int) CubeFace.FRONT],
            _symbols[(int) CubeFace.DOWN],
            _symbols[(int) CubeFace.RIGHT],
            _symbols[(int) CubeFace.UP],
            _symbols[(int) CubeFace.LEFT],
        };
        _rotations = new[] {
            (4 + _rotations[(int) CubeFace.BACK]) % 4,
            (4 + _rotations[(int) CubeFace.FRONT]) % 4,
            (4 + _rotations[(int) CubeFace.DOWN] + 2) % 4,
            (4 + _rotations[(int) CubeFace.RIGHT] + 1) % 4,
            (4 + _rotations[(int) CubeFace.UP] + 2) % 4,
            (4 + _rotations[(int) CubeFace.LEFT] - 1) % 4,
        };
    }

    public void RotateDown() {
        _colors = new[] {
            _colors[(int) CubeFace.FRONT],
            _colors[(int) CubeFace.BACK],
            _colors[(int) CubeFace.UP],
            _colors[(int) CubeFace.RIGHT],
            _colors[(int) CubeFace.DOWN],
            _colors[(int) CubeFace.LEFT],
        };
        _symbols = new[] {
            _symbols[(int) CubeFace.FRONT],
            _symbols[(int) CubeFace.BACK],
            _symbols[(int) CubeFace.UP],
            _symbols[(int) CubeFace.RIGHT],
            _symbols[(int) CubeFace.DOWN],
            _symbols[(int) CubeFace.LEFT],
        };
        _rotations = new[] {
            (4 + _rotations[(int) CubeFace.FRONT] + 2) % 4,
            (4 + _rotations[(int) CubeFace.BACK] + 2) % 4,
            (4 + _rotations[(int) CubeFace.UP]) % 4,
            (4 + _rotations[(int) CubeFace.RIGHT] - 1) % 4,
            (4 + _rotations[(int) CubeFace.DOWN]) % 4,
            (4 + _rotations[(int) CubeFace.LEFT] + 1) % 4,
        };
    }

    public void RotateLeft() {
        _colors = new[] {
            _colors[(int) CubeFace.RIGHT],
            _colors[(int) CubeFace.LEFT],
            _colors[(int) CubeFace.BACK],
            _colors[(int) CubeFace.DOWN],
            _colors[(int) CubeFace.FRONT],
            _colors[(int) CubeFace.UP],
        };
        _symbols = new[] {
            _symbols[(int) CubeFace.RIGHT],
            _symbols[(int) CubeFace.LEFT],
            _symbols[(int) CubeFace.BACK],
            _symbols[(int) CubeFace.DOWN],
            _symbols[(int) CubeFace.FRONT],
            _symbols[(int) CubeFace.UP],
        };
        _rotations = new[] {
            (4 + _rotations[(int) CubeFace.RIGHT] - 1) % 4,
            (4 + _rotations[(int) CubeFace.LEFT] + 1) % 4,
            (4 + _rotations[(int) CubeFace.BACK] - 1) % 4,
            (4 + _rotations[(int) CubeFace.DOWN] + 1) % 4,
            (4 + _rotations[(int) CubeFace.FRONT] + 1) % 4,
            (4 + _rotations[(int) CubeFace.UP] - 1) % 4,
        };
    }

    public void RotateRight() {
        _colors = new[] {
            _colors[(int) CubeFace.LEFT],
            _colors[(int) CubeFace.RIGHT],
            _colors[(int) CubeFace.BACK],
            _colors[(int) CubeFace.UP],
            _colors[(int) CubeFace.FRONT],
            _colors[(int) CubeFace.DOWN],
        };
        _symbols = new[] {
            _symbols[(int) CubeFace.LEFT],
            _symbols[(int) CubeFace.RIGHT],
            _symbols[(int) CubeFace.BACK],
            _symbols[(int) CubeFace.UP],
            _symbols[(int) CubeFace.FRONT],
            _symbols[(int) CubeFace.DOWN],
        };
        _rotations = new[] {
            (4 + _rotations[(int) CubeFace.LEFT] + 1) % 4,
            (4 + _rotations[(int) CubeFace.RIGHT] - 1) % 4,
            (4 + _rotations[(int) CubeFace.BACK] + 1) % 4,
            (4 + _rotations[(int) CubeFace.UP] + 1) % 4,
            (4 + _rotations[(int) CubeFace.FRONT] - 1) % 4,
            (4 + _rotations[(int) CubeFace.DOWN] - 1) % 4,
        };
    }

    public Cube Clone() {
        return new Cube(_colors.Clone() as int[], _symbols.Clone() as int[], _rotations.Clone() as int[]);
    }

    public static Cube MakeCube(int netIndex, IList<List<int>> colors, IList<List<int>> symbols, int baseRotation,
        bool flipped) {
        Cube cube;

        switch (netIndex) {
            case 0:
                if (!flipped) {
                    cube = new Cube(new[] {
                        colors[2][0],
                        colors[4][0],
                        colors[3][0],
                        colors[2][1],
                        colors[1][1],
                        colors[0][1],
                    }, new[] {
                        symbols[2][0],
                        symbols[4][0],
                        symbols[3][0],
                        symbols[2][1],
                        symbols[1][1],
                        symbols[0][1],
                    }, new[] {0, 2, 0, 1, 1, 1}.Select(x => (x - baseRotation + 4) % 4).ToArray());
                } else {
                    cube = new Cube(new[] {
                        colors[2][0],
                        colors[4][0],
                        colors[3][0],
                        colors[0][1],
                        colors[1][1],
                        colors[2][1],
                    }, new[] {
                        symbols[2][0],
                        symbols[4][0],
                        symbols[3][0],
                        symbols[0][1],
                        symbols[1][1],
                        symbols[2][1],
                    }, new[] {0, 2, 0, 3, 3, 3}.Select(x => (x - baseRotation + 4) % 4).ToArray());
                }

                break;
            case 1:
                if (!flipped) {
                    cube = new Cube(new[] {
                        colors[1][1],
                        colors[3][1],
                        colors[2][1],
                        colors[1][2],
                        colors[0][0],
                        colors[1][0],
                    }, new[] {
                        symbols[1][1],
                        symbols[3][1],
                        symbols[2][1],
                        symbols[1][2],
                        symbols[0][0],
                        symbols[1][0],
                    }, new[] {0, 2, 0, 1, 3, 3}.Select(x => (x - baseRotation + 4) % 4).ToArray());
                } else {
                    cube = new Cube(new[] {
                        colors[1][1],
                        colors[3][1],
                        colors[2][1],
                        colors[1][0],
                        colors[0][0],
                        colors[1][2],
                    }, new[] {
                        symbols[1][1],
                        symbols[3][1],
                        symbols[2][1],
                        symbols[1][0],
                        symbols[0][0],
                        symbols[1][2],
                    }, new[] {0, 2, 0, 1, 1, 3}.Select(x => (x - baseRotation + 4) % 4).ToArray());
                }

                break;
            case 2:
                if (!flipped) {
                    cube = new Cube(new[] {
                        colors[1][1],
                        colors[3][1],
                        colors[2][1],
                        colors[2][2],
                        colors[0][0],
                        colors[1][0],
                    }, new[] {
                        symbols[1][1],
                        symbols[3][1],
                        symbols[2][1],
                        symbols[2][2],
                        symbols[0][0],
                        symbols[1][0],
                    }, new[] {0, 2, 0, 0, 3, 3}.Select(x => (x - baseRotation + 4) % 4).ToArray());
                } else {
                    cube = new Cube(new[] {
                        colors[1][1],
                        colors[3][1],
                        colors[2][1],
                        colors[1][0],
                        colors[0][0],
                        colors[2][2],
                    }, new[] {
                        symbols[1][1],
                        symbols[3][1],
                        symbols[2][1],
                        symbols[1][0],
                        symbols[0][0],
                        symbols[2][2],
                    }, new[] {0, 2, 0, 1, 1, 0}.Select(x => (x - baseRotation + 4) % 4).ToArray());
                }

                break;
            default:
                throw new ArgumentException();
        }

        return cube;
    }
}