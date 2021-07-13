using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;
using System.Text.RegularExpressions;
using Random = UnityEngine.Random;

public class WalkingCubeModule : MonoBehaviour
{
    // Constants
    private const int GRID_SIZE = 4;
    private const int PATH_LENGTH_MIN = 5;
    private const int PATH_LENGTH_MAX = 6;
    private const string MODULE_NAME = "Walking Cube";

    private static readonly string[] SUCCESS_MESSAGES =
        {"ALL\nDONE", "NICE\nWORK", "GO\nPRO!", "NICE\nJOB", "DONE", "NICE!", "WOW!"};

    private static readonly string[] COLOR_NAMES = {
        "RED", "GREEN", "BLUE", "CYAN", "MAGENTA", "YELLOW", "WHITE", "ORANGE"
    };

    private static readonly Color[] CB_COLORS = {
        Color.white, Color.black, Color.white, Color.black, Color.white, Color.black, Color.black, Color.white,
    };

    private static readonly int[][] DEFAULT_COLORS = {
        new[] {0, 0, 4, 6, 2},
        new[] {2, 0, 3, 4, 0},
        new[] {5, 5, 6, 7, 5},
        new[] {1, 2, 4, 1, 6},
        new[] {2, 2, 1, 4, 2}
    };

    private static readonly int[][] DEFAULT_SYMBOLS = {
        new[] {70, 80, 75, 117, 93},
        new[] {10, 11, 12, 111, 14},
        new[] {0, 112, 2, 105, 20},
        new[] {62, 16, 103, 18, 19},
        new[] {4, 21, 22, 23, 24}
    };

    // Module ID
    private static int _nextModuleId;
    private int _moduleId;

    // Module stuff
    public KMRuleSeedable ruleSeedable;
    public KMBombModule bombModule;
    public KMModSettings modSettings;
    public KMAudio bombAudio;

    // Colors and symbols
    public Material[] colorMaterials;
    public MeshRenderer gridBackgroundRenderer;
    public Texture[] symbolTextures;
    public GameObject[] smallSymbols;

    // Button hooks
    public KMSelectable[] gridButtons;
    public KMSelectable[] symbolButtons;

    // Colorblind mode
    public KMColorblindMode colorblindMode;
    public GameObject[] colorblindConditionalObjects;

    // Cube rotation
    public Transform cubeTransform;
    public Transform cubeContainerTransform;
    public Transform cubeParentTransform;
    public MeshRenderer cubeRenderer;

    // Symbol rotation
    public GameObject[] symbolObjects;
    public TextMesh pageText;
    public MeshRenderer[] symbolEdges;
    public Material redEdgeMaterial;
    public Material greenEdgeMaterial;
    public Material blueEdgeMaterial;

    // DEBUG: seed
    public int seed;

    // Solution
    private int[][] _symbolGrid;
    private int[][] _colorGrid;
    private List<CubeRotation> _path;
    private List<Pair<int, int>> _locations;
    private List<Pair<int, int>> _locationsSol;
    private Vector2Int _startingPos;
    private Vector2Int _netPos;
    private int[][] _solutionSymbols;
    private int[][] _solutionRotations;
    private bool _solved = false;
    private bool _onSolveCycle = false;
    private string _successString;
    private Dictionary<int, string> _symbolPositions = new Dictionary<int, string>();

    // Cube rotation variables
    private bool _forwards = true;
    private float _cubeSpeedMultiplier = 1.0f;
    private bool _tpRotating;
    private bool _currentlyWalking;
    private Vector3[] _tpAxes = { new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 0, -1) };

    private readonly Queue<CubeRotation> _movements = new Queue<CubeRotation>();

    // Symbol rotation variables
    private readonly Queue<RotateTask>[] _queues = new Queue<RotateTask>[5];
    private int _symbolPage = 0;
    private readonly int[] _symbolRotations = new int[5];
    private int _selectedSymbol = -1;

    private sealed class RotateTask
    {
        public int From;
        public int To;
    }

    // Use this for initialization
    private void Start()
    {
        // Initialization
        _moduleId = ++_nextModuleId;
        for (var i = 0; i < 5; i++)
        {
            _queues[i] = new Queue<RotateTask>();
            _symbolRotations[i] = 0;
            StartCoroutine(RotateSymbol(i));
        }

        // Solution generation and setup of puzzle
        LoadModSettings();
        GenerateManual();
        GeneratePuzzle();
        SetupCube();
        UpdateSymbols();

        // Hooks
        bombModule.OnActivate += delegate { StartCoroutine(RotateCube()); };
        gridButtons.Peek((button, i) => button.OnInteract += delegate
        {
            button.AddInteractionPunch(0.3f);
            if (!_solved)
            {
                HandleGridPress(i, i % 4, i / 4);
            }

            return false;
        });
        symbolButtons.Peek((button, i) => button.OnInteract += delegate
        {
            button.AddInteractionPunch(0.5f);
            if (!_solved)
            {
                HandleSymbolPress(i);
            }

            return false;
        });
        smallSymbols.Peek(obj => obj.SetActive(false));
    }

    private void HandleGridPress(int index, int col, int row)
    {
        bombAudio.PlaySoundAtTransform("GridPlacement", transform);
        Debug.LogFormat("[{0} #{1}] Pressed: {2}{3}", MODULE_NAME, _moduleId, "ABCD"[col], row + 1);

        // No symbol selected
        if (_selectedSymbol == -1)
        {
            Debug.LogFormat("[{0} #{1}] Strike! No symbol is currently selected", MODULE_NAME, _moduleId);
            bombModule.HandleStrike();
            return;
        }

        // Invalid location
        if (_solutionSymbols[row][col] == -1)
        {
            Debug.LogFormat("[{0} #{1}] Strike! Pressed location not in the path of the cube", MODULE_NAME, _moduleId);
            bombModule.HandleStrike();
            return;
        }

        // Invalid symbol
        if (_solutionSymbols[row][col] != _symbolGrid[_selectedSymbol][_symbolPage])
        {
            Debug.LogFormat("[{0} #{1}] Strike! Wrong symbol", MODULE_NAME, _moduleId);
            bombModule.HandleStrike();
            return;
        }

        // Invalid rotation
        if (_solutionRotations[row][col] != _symbolRotations[_selectedSymbol])
        {
            Debug.LogFormat("[{0} #{1}] Strike! Wrong rotation", MODULE_NAME, _moduleId);
            bombModule.HandleStrike();
            return;
        }

        // // Already pressed
        if (_locationsSol.Contains(new Pair<int, int>(row, col)))
        {
            Debug.LogFormat("[{0} #{1}] Strike! Already placed that symbol", MODULE_NAME, _moduleId);
            bombModule.HandleStrike();
            return;
        }

        // Valid!
        Debug.LogFormat("[{0} #{1}] Correct! Correct symbol placed", MODULE_NAME, _moduleId);
        _locationsSol.Add(new Pair<int, int>(row, col));
        smallSymbols[row * GRID_SIZE + col].SetActive(true);
        smallSymbols[row * GRID_SIZE + col].GetComponent<MeshRenderer>().material.mainTexture =
            symbolTextures[_symbolGrid[_selectedSymbol][_symbolPage]];
        smallSymbols[row * GRID_SIZE + col].transform.localRotation =
            Quaternion.Euler(90, 0, 90 * _solutionRotations[row][col]);

        if (_locationsSol.Count == _locations.Count)
        {
            bombAudio.PlaySoundAtTransform("SolveSound", transform);
            Debug.LogFormat("[{0} #{1}] Solved!", MODULE_NAME, _moduleId);
            bombModule.HandlePass();
            _solved = true;

            // Remove symbol displays
            _symbolPage = -1;
            UpdateSymbols();
        }
    }

    private IEnumerator SolveAnimation()
    {
        const float duration = 3f;

        // Setup
        pageText.text = _successString;
        for (var i = 0; i < 5; i++)
        {
            symbolEdges[i].materials = new[]
                {blueEdgeMaterial, symbolEdges[i].materials[1]};
        }

        var initColor = gridBackgroundRenderer.materials[1].color;
        var targetColor = new Color(0x1e / 255f, 0x8d / 255f, 0x1e / 255f, 0xff / 255f);
        var initScale = symbolObjects[0].transform.localScale;
        var initSmalLScale = smallSymbols[0].transform.localScale;

        var elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;

            // Spin out symbols
            var t = Easing.InOutSine(Mathf.Min(elapsed, duration), 0, 1, duration);
            for (var i = 0; i < 5; i++)
            {
                symbolObjects[i].transform.localRotation = Quaternion.Euler(180, 0, 360 * t);
                symbolObjects[i].transform.localScale = initScale * (1 - t);
            }

            foreach (var smallSymbol in smallSymbols)
            {
                smallSymbol.transform.localScale = initSmalLScale * (1 - t);
            }

            // Make screen green
            gridBackgroundRenderer.materials[1].color = Color.Lerp(initColor, targetColor, t);
        }
    }

    private void HandleSymbolPress(int index)
    {
        if (index == 5)
        {
            bombAudio.PlaySoundAtTransform("PageChange", transform);
            for (var i = 0; i < 5; i++)
            {
                _queues[i].Clear();
                if (_symbolRotations[i] != 0)
                {
                    _queues[i].Enqueue(new RotateTask { From = _symbolRotations[i], To = 0 });
                    _symbolRotations[i] = 0;
                }
            }

            _symbolPage = (_symbolPage + 1) % 5;
            pageText.text = "Page\n" + (_symbolPage + 1);
            _selectedSymbol = -1;

            UpdateSymbols();
            return;
        }

        bombAudio.PlaySoundAtTransform("RotateSymbol", transform);
        if (_selectedSymbol != index)
        {
            _selectedSymbol = index;
            UpdateSymbols();
        }
        else
        {
            _queues[index].Enqueue(new RotateTask
            { From = _symbolRotations[index], To = (_symbolRotations[index] + 1) % 4 });
            _symbolRotations[index] = (_symbolRotations[index] + 1) % 4;
        }
    }

    private void UpdateSymbols()
    {
        if (_symbolPage == -1)
        {
            StartCoroutine(SolveAnimation());
            return;
        }

        for (var i = 0; i < 5; i++)
        {
            symbolObjects[i].GetComponent<MeshRenderer>().material.mainTexture =
                symbolTextures[_symbolGrid[i][_symbolPage]];
            symbolEdges[i].materials = new[]
                {_selectedSymbol == i ? greenEdgeMaterial : redEdgeMaterial, symbolEdges[i].materials[1]};
        }
    }

    private class Settings
    {
#pragma warning disable 649
        public float CubeSpeedMultiplier;
#pragma warning restore 649
    }

    private void LoadModSettings()
    {
        var x = JsonConvert.DeserializeObject<Settings>(modSettings.Settings);
        _cubeSpeedMultiplier = x.CubeSpeedMultiplier;
    }

    private void SetupCube()
    {
        cubeTransform.localPosition = new Vector3(_startingPos.x - GRID_SIZE / 2f + 0.5f, 0.5f,
            -_startingPos.y + GRID_SIZE / 2f - 0.5f);

        colorblindConditionalObjects.Peek(o => { o.SetActive(colorblindMode.ColorblindModeActive); });
    }

    private void GeneratePuzzle()
    {
        if (seed != 0)
        {
            Random.InitState(seed);
        }

        // Starting position 
        int startPosX = Random.Range(0, GRID_SIZE), startPosY = Random.Range(0, GRID_SIZE);
        var numMoves = Random.Range(PATH_LENGTH_MIN, PATH_LENGTH_MAX + 1) + 1;

        // Generate path
        var visitedLocations = new List<Pair<int, int>>();
        var path = new List<CubeRotation>();
        var valid = false;
        var numTries = 10;
        startOver:
        while (!valid)
        {
            // Setup list
            int x = startPosX, y = startPosY;
            visitedLocations.Clear();
            path.Clear();
            visitedLocations.Add(new Pair<int, int>(x, y));

            // Pick cube directions
            for (var i = 0; i < numMoves - 1;)
            {
                // Randomly choose next movement
                var dir = RandomCubeDirection(x, y, path.Count > 0 ? path[path.Count - 1] : (CubeRotation?)null);
                switch (dir)
                {
                    case CubeRotation.RIGHT:
                        ++x;
                        break;
                    case CubeRotation.LEFT:
                        --x;
                        break;
                    case CubeRotation.UP:
                        --y;
                        break;
                    case CubeRotation.DOWN:
                        ++y;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Check for duplicates
                if (visitedLocations.Contains(new Pair<int, int>(x, y)))
                {
                    if (--numTries <= 0)
                    {
                        goto startOver;
                    }

                    switch (dir)
                    {
                        case CubeRotation.RIGHT:
                            --x;
                            break;
                        case CubeRotation.LEFT:
                            ++x;
                            break;
                        case CubeRotation.UP:
                            ++y;
                            break;
                        case CubeRotation.DOWN:
                            --y;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    continue;
                }

                // Next location
                visitedLocations.Add(new Pair<int, int>(x, y));
                path.Add(dir);
                ++i;
            }

            // Valid path
            valid = true;
        }

        // Save cube movement
        _path = path;
        _locations = visitedLocations;
        _locationsSol = new List<Pair<int, int>>();
        _startingPos = new Vector2Int(startPosX, startPosY);
        Debug.LogFormat("[{0} #{1}] Cube starting position: {2}{3}", MODULE_NAME, _moduleId, "ABCD"[startPosX],
            startPosY + 1);
        Debug.LogFormat("[{0} #{1}] Cube path (including starting position): {2}", MODULE_NAME, _moduleId,
            _locations.Select(loc => string.Format(@"{0}{1}", "ABCD"[loc.First],
                loc.Second + 1)).Join(", "));
        Debug.LogFormat("[{0} #{1}] Cube rotations: {2}", MODULE_NAME, _moduleId,
            _path.Join(", "));

        // Choose cube net
        var netIndex = Random.Range(0, Cube.NET_STRINGS.Length);
        var net = Cube.NET_STRINGS[netIndex];
        var flipped = Random.Range(0f, 1f) > 0.5f;
        var rotation = Random.Range(0, netIndex == 0 ? 2 : 4);
        var fixedNet = (flipped ? net.MirrorVertical() : net).Rotate(rotation);
        Debug.LogFormat("[{0} #{1}] Chosen net:", MODULE_NAME, _moduleId);
        fixedNet.Replace(' ', '░').Replace('X', '█').Split('\n')
            .Peek(line => Debug.LogFormat("[{0} #{1}]     {2}", MODULE_NAME, _moduleId, line));

        // Choose net location
        var splitNet = fixedNet.Split('\n');
        _netPos = new Vector2Int(Random.Range(0, 5 - splitNet[0].Length + 1), Random.Range(0, 5 - splitNet.Length + 1));
        Debug.LogFormat("[{0} #{1}] Net position in manual: {2}{3}", MODULE_NAME, _moduleId, "ABCDE"[_netPos.x],
            _netPos.y + 1);

        // Find and log net colors
        var netColors =
            splitNet.Select((r, row) =>
                    r.Select((val, col) => val == ' ' ? -2 : _colorGrid[row + _netPos.y][col + _netPos.x]).ToArray())
                .ToArray();
        Debug.LogFormat("[{0} #{1}] Net colors:", MODULE_NAME, _moduleId);
        netColors.Peek(row => Debug.LogFormat("[{0} #{1}]     {2}", MODULE_NAME, _moduleId,
            row.Select(v => v == -2 ? '.' : v == -1 ? 'X' : COLOR_NAMES[v][0]).Join("")));

        // Find and log net symbols
        var netSymbols =
            splitNet.Select((r, row) =>
                    r.Select((val, col) => val == ' ' ? -2 : _symbolGrid[row + _netPos.y][col + _netPos.x]).ToArray())
                .ToArray();
        Debug.LogFormat("[{0} #{1}] Net symbols:", MODULE_NAME, _moduleId);
        netSymbols.Peek(row => Debug.LogFormat("[{0} #{1}]     {2}", MODULE_NAME, _moduleId,
            row.Select(v => v == -2 ? ".." : _symbolPositions[v]).Join(" ")));

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

        // Randomize initial cube rotation
        while (Random.value > 0.5f)
        {
            switch (Random.Range(0, 4))
            {
                case 0:
                    cube.RotateUp();
                    break;
                case 1:
                    cube.RotateLeft();
                    break;
                case 2:
                    cube.RotateRight();
                    break;
                case 3:
                    cube.RotateDown();
                    break;
            }
        }

        // Log initial orientation of cube
        Debug.LogFormat("[{0} #{1}] Initial orientation of cube: {2}", MODULE_NAME, _moduleId,
            Enumerable.Range(0, 6).Select((i) =>
                (CubeFace)i + "=" +
                (cube.GetColor((CubeFace)i) == -1 ? "NONE" : COLOR_NAMES[cube.GetColor((CubeFace)i)])).Join(", "));

        // Setup solution
        _solutionSymbols = new int[GRID_SIZE][];
        _solutionRotations = new int[GRID_SIZE][];
        for (var i = 0; i < GRID_SIZE; i++)
        {
            _solutionSymbols[i] = new int[GRID_SIZE];
            _solutionRotations[i] = new int[GRID_SIZE];
            for (var j = 0; j < GRID_SIZE; j++)
            {
                _solutionSymbols[i][j] = -1;
                _solutionRotations[i][j] = -1;
            }
        }

        // Determine solution
        var solCube = cube.Clone();
        for (var i = 0; i < _locations.Count; i++)
        {
            // Get information
            var symbol = solCube.GetSymbol(CubeFace.DOWN);
            var rot = solCube.GetRotation(CubeFace.DOWN);
            var loc = _locations[i];

            // Record solution
            _solutionSymbols[loc.Second][loc.First] = symbol;
            _solutionRotations[loc.Second][loc.First] = rot;
            Debug.LogFormat("[{0} #{1}] Solution {6}/{7}: {2}{3} is symbol {4} with orientation {5}", MODULE_NAME,
                _moduleId,
                "ABCD"[loc.First], loc.Second + 1, _symbolPositions[symbol], "NWSE"[rot], i + 1, _locations.Count
            );

            // Rotate cube accordingly
            if (i < path.Count)
            {
                solCube.Rotate(_path[i]);
            }
        }

        // Setup cube colors
        cubeRenderer.materials = new[] {
            colorMaterials[cube.GetColor(CubeFace.FRONT) + 1],
            colorMaterials[cube.GetColor(CubeFace.BACK) + 1],
            colorMaterials[cube.GetColor(CubeFace.LEFT) + 1],
            colorMaterials[cube.GetColor(CubeFace.RIGHT) + 1],
            colorMaterials[cube.GetColor(CubeFace.UP) + 1],
            colorMaterials[cube.GetColor(CubeFace.DOWN) + 1],
            cubeRenderer.materials[6]
        };

        // Colorblind mode labels
        for (var i = 0; i < colorblindConditionalObjects.Length; i++)
        {
            var color = cube.GetColor((CubeFace)i);
            colorblindConditionalObjects[i].GetComponent<TextMesh>().text =
                color == -1 ? "" : COLOR_NAMES[color][0] + "";
            colorblindConditionalObjects[i].GetComponent<TextMesh>().color =
                color == -1 ? Color.black : CB_COLORS[color];
        }

        // Choose success text
        _successString = SUCCESS_MESSAGES.PickRandom();
    }

    private static CubeRotation RandomCubeDirection(int x, int y, CubeRotation? prev)
    {
        var poss = new List<CubeRotation>();

        if (x > 0 && prev != CubeRotation.RIGHT)
        {
            poss.Add(CubeRotation.LEFT);
        }

        if (y > 0 && prev != CubeRotation.DOWN)
        {
            poss.Add(CubeRotation.UP);
        }

        if (x < GRID_SIZE - 1 && prev != CubeRotation.LEFT)
        {
            poss.Add(CubeRotation.RIGHT);
        }

        if (y < GRID_SIZE - 1 && prev != CubeRotation.UP)
        {
            poss.Add(CubeRotation.DOWN);
        }

        return poss.PickRandom();
    }

    private bool EnqueueCubeMovements()
    {
        if (_solved && !_onSolveCycle && _forwards)
        {
            for (var i = 0; i < _startingPos.x; i++)
            {
                _movements.Enqueue(CubeRotation.LEFT);
            }

            for (var i = 0; i < _startingPos.y; i++)
            {
                _movements.Enqueue(CubeRotation.UP);
            }

            _onSolveCycle = true;
            return true;
        }
        else if (_onSolveCycle)
        {
            for (var i = 0; i < 3; i++)
            {
                _movements.Enqueue(CubeRotation.RIGHT);
            }

            for (var i = 0; i < 3; i++)
            {
                _movements.Enqueue(CubeRotation.DOWN);
            }

            for (var i = 0; i < 3; i++)
            {
                _movements.Enqueue(CubeRotation.LEFT);
            }

            for (var i = 0; i < 3; i++)
            {
                _movements.Enqueue(CubeRotation.UP);
            }
        }
        else if (_forwards)
        {
            foreach (var t in _path)
            {
                _movements.Enqueue(t);
            }
        }
        else
        {
            for (var i = _path.Count - 1; i >= 0; i--)
            {
                switch (_path[i])
                {
                    case CubeRotation.RIGHT:
                        _movements.Enqueue(CubeRotation.LEFT);
                        break;
                    case CubeRotation.LEFT:
                        _movements.Enqueue(CubeRotation.RIGHT);
                        break;
                    case CubeRotation.UP:
                        _movements.Enqueue(CubeRotation.DOWN);
                        break;
                    case CubeRotation.DOWN:
                        _movements.Enqueue(CubeRotation.UP);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        _forwards = !_forwards;

        return false;
    }

    private void GenerateManual()
    {
        // Generate manual
        var rnd = ruleSeedable.GetRNG();

        // Colors
        if (rnd.Seed != 1)
        {
            var shuffledColors = rnd.ShuffleFisherYates(Enumerable.Range(0, COLOR_NAMES.Length).ToList());
            var res = DEFAULT_COLORS.Select(row => row.Select(val => val == -1 ? -1 : shuffledColors[val]).ToList());
            if (rnd.NextDouble() > 0.5)
            {
                res = res.MirrorHorizontal();
            }

            if (rnd.NextDouble() > 0.5)
            {
                res = res.MirrorVertical();
            }

            var rotCount = rnd.Next(0, 4);
            _colorGrid = res.Select(l => l.ToArray()).ToArray().Rotate(rotCount).Select(l => l.ToArray())
                .ToArray();
        }
        else
        {
            _colorGrid = DEFAULT_COLORS;
        }

        // Symbols
        var symbols = Enumerable.Range(0, symbolTextures.Length).ToList();
        if (rnd.Seed != 1)
        {
            symbols = rnd.ShuffleFisherYates(symbols);
        }

        _symbolGrid = DEFAULT_SYMBOLS.Select(row => row.Select(val => symbols[val]).ToArray()).ToArray();

        // Logging helper
        for (var i = 0; i < _symbolGrid.Length; i++)
        {
            for (var j = 0; j < _symbolGrid[i].Length; j++)
            {
                _symbolPositions[_symbolGrid[i][j]] = "" + "ABCDE"[j] + (i + 1);
            }
        }

        // Log manual
        // Debug.LogFormat("[{0} #{1}] Manual for rule seed {2}:", MODULE_NAME, _moduleId, rnd.Seed);
        // _symbolGrid.Peek(row => Debug.LogFormat("[{0} #{1}]     {2}", MODULE_NAME, _moduleId, row.Join("    ")));
        // Debug.LogFormat("[{0} #{1}] Colors in reading order:", MODULE_NAME, _moduleId);
        // _colorGrid.Peek(row => Debug.LogFormat("[{0} #{1}]     {2}", MODULE_NAME, _moduleId,
        //     row.Select(c => c != -1 ? COLOR_NAMES[c][0] : '.').Join("")));
    }

    private IEnumerator RotateCube()
    {
        yield return null;

        const float duration = 0.5f;
        var pacing = 2f;

        while (true)
        {
            yield return null;
            if (_movements.Count == 0)
            {
                if (EnqueueCubeMovements())
                {
                    pacing = 0f;
                }
                yield return new WaitForSeconds(pacing * _cubeSpeedMultiplier);
                continue;
            }

            var axis = _movements.Dequeue();

            Vector3 movementAxis;
            Vector3 rotationAxis;
            switch (axis)
            {
                case CubeRotation.RIGHT:
                    movementAxis = Vector3.right;
                    rotationAxis = Vector3.back;
                    break;
                case CubeRotation.LEFT:
                    movementAxis = Vector3.left;
                    rotationAxis = Vector3.forward;
                    break;
                case CubeRotation.UP:
                    movementAxis = Vector3.forward;
                    rotationAxis = Vector3.right;
                    break;
                case CubeRotation.DOWN:
                    movementAxis = Vector3.back;
                    rotationAxis = Vector3.left;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var startPosition = cubeTransform.localPosition;
            var startRotation = cubeTransform.localRotation;
            var rotationPoint = startPosition + (movementAxis + Vector3.down) / 2 * cubeTransform.localScale.y;

            var elapsed = 0f;
            var thisRotDuration = duration * _cubeSpeedMultiplier;
            _currentlyWalking = true;
            while (elapsed < thisRotDuration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                var t = Easing.InOutSine(Mathf.Min(elapsed, thisRotDuration), 0, 1, thisRotDuration);
                cubeTransform.localRotation = Quaternion.AngleAxis(90 * t, rotationAxis) * startRotation;
                // cube.localRotation = Quaternion.Slerp(startRotation,
                //     startRotation * Quaternion.AngleAxis(90, rotationAxis), t);
                cubeTransform.localPosition = RotatePointAroundPivot(startPosition, rotationPoint,
                    Quaternion.AngleAxis(90 * t, rotationAxis));
            }
            _currentlyWalking = false;
            yield return new WaitForSeconds(pacing * _cubeSpeedMultiplier);
            while (_tpRotating)
                yield return null;
        }
    }

    private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
    {
        return rotation * (point - pivot) + pivot;
    }

    private IEnumerator RotateSymbol(int ix)
    {
        const float duration = .15f;

        while (true)
        {
            yield return null;
            if (_queues[ix].Count == 0)
                continue;

            var elem = _queues[ix].Dequeue();
            var elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                var t = Easing.OutSine(Mathf.Min(elapsed, duration), 0, 1, duration);
                symbolObjects[ix].transform.localRotation =
                    Quaternion.Slerp(Quaternion.Euler(180, 0, elem.From * 90), Quaternion.Euler(180, 0, elem.To * 90),
                        t);
            }
        }
    }

    string[] GetCoordinates(int gridSize)
    {
        return Enumerable.Range(0, gridSize * gridSize).Select(x => "ABCDEFGHIJKLMNOPQRSTUVWXZ"[x % gridSize] + "" + (x / gridSize + 1)).ToArray();
    }
    int CoordToInt(string coordinate, int gridSize)
    {
        return Array.IndexOf(GetCoordinates(gridSize), coordinate);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} rotate cube> to show more faces of the cube. Use <!{0} place A1 in B3> to place the symbol from A1 in the table in cell B3 of the module. Use <!{0} rotate A1 90/180/270 cw/ccw> to rotate that symbol that many degrees in that direction. The first coordinate can be removed to use the currently selected symbol. Placement commands can be chained with commas.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] regices = new string[] { @"^PLACE\s+([A-E][1-5]\s+)?IN\s+([A-D][1-4])$", @"^ROTATE\s+([A-E][1-5]\s+)?(90|180|270)\s+(C?CW)$" };
        List<string> commands = command.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToUpperInvariant()).ToList();
        if (commands.Count == 1 && Regex.IsMatch(commands[0], @"^ROTATE\s+CUBE$"))
        {
            yield return null;
            if (!_tpRotating)
                StartCoroutine(TPCubeRotate());
            while (_tpRotating)
                yield return "trycancel Focus on rotating cube cancelled.";
        }
        else if (commands.Any(cmd => !regices.Any(rx => Regex.IsMatch(cmd, rx)))) //If any command doesn't fit any of the regices, abort.
            yield break;
        foreach (string subcommand in commands)
        {
            Match[] matches = regices.Select(x => Regex.Match(subcommand, x)).ToArray();
            if (matches[0].Success) //place (sym) in (coord)
            {
                GroupCollection groups = matches[0].Groups;
                if (groups[1].Length == 0 && _selectedSymbol == -1)
                {
                    yield return "sendtochaterror You cannot place the given symbol because no symbol is selected.";
                    yield break;
                }
                yield return null;
                if (groups[1].Length != 0)
                {
                    while (_symbolPage != (groups[1].Value[0] - 'A'))
                    {
                        symbolButtons[5].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                    if (_selectedSymbol != groups[1].Value[1] - '1')
                        symbolButtons[groups[1].Value[1] - '1'].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
                gridButtons[CoordToInt(groups[2].Value, 4)].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (matches[1].Success)
            {
                GroupCollection groups = matches[1].Groups;
                if (groups[1].Length == 0 && _selectedSymbol == -1)
                {
                    yield return "sendtochaterror You cannot rotate the given symbol because no symbol is selected.";
                    yield break;
                }
                yield return null;
                if (groups[1].Length != 0)
                {
                    while (_symbolPage != (groups[1].Value[0] - 'A'))
                    {
                        symbolButtons[5].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                    if (_selectedSymbol != groups[1].Value[1] - '1')
                        symbolButtons[groups[1].Value[1] - '1'].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
                int rotCount = int.Parse(groups[2].Value) / 90;
                if (groups[3].Value == "CW")
                    rotCount = 4 - rotCount; //If the direction is cw, flip the direction.
                KMSelectable targetButton = (groups[1].Length == 0) ? symbolButtons[_selectedSymbol] : symbolButtons[groups[1].Value[1] - '1'];
                for (int i = 0; i < rotCount; i++)
                {
                    targetButton.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
    }

    IEnumerator TPCubeRotate()
    {
        while (_currentlyWalking)
            yield return null;
        _tpRotating = true;
        Vector3 initPos = cubeTransform.localPosition;
        cubeContainerTransform.localPosition = initPos;
        cubeTransform.SetParent(cubeContainerTransform);
        Transform tf = cubeContainerTransform;
        while (tf.localPosition.y < 1)
        {
            tf.localPosition += Vector3.up * 0.5f * Time.deltaTime;
            yield return null;
        }
        for (int rotNum = 0; rotNum < 4; rotNum++)
        {
            float delta1 = 0;
            while (delta1 < 1)
            {
                delta1 += Time.deltaTime;
                tf.localEulerAngles = Vector3.Lerp(Vector3.zero, _tpAxes[rotNum] * 90, delta1);
                yield return null;
            }
            float delta2 = 0;
            while (delta2 < 1)
            {
                delta2 += Time.deltaTime;
                tf.localEulerAngles = Vector3.Lerp(_tpAxes[rotNum] * 90, Vector3.zero, delta2);
                yield return null;
            }
        }
        while (tf.localPosition.y > 0.5f)
        {
            tf.localPosition += Vector3.down * 0.5f * Time.deltaTime;
            yield return null;
        }
        cubeTransform.SetParent(cubeParentTransform);
        _tpRotating = false;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!_solved)
        {
            for (int displayIx = 0; displayIx < 5; displayIx++)
                for (int gridIx = 0; gridIx < 16; gridIx++)
                    if (_solutionSymbols[gridIx / 4][gridIx % 4] == _symbolGrid[displayIx][_symbolPage] && !_locationsSol.Contains(new Pair<int, int>(gridIx / 4, gridIx % 4)))
                    {
                        do
                        {
                            symbolButtons[displayIx].OnInteract();
                            yield return new WaitForSeconds(0.1f);
                        } while (_symbolRotations[displayIx] != _solutionRotations[gridIx / 4][gridIx % 4]);
                        gridButtons[gridIx].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                        if (_solved)
                            yield break;
                    }
            symbolButtons[5].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }
}
