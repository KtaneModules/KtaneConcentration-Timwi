using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Concentration;
using UnityEngine;

/// <summary>On the Subject of Concentration Created by Timwi</summary>
public class ConcentrationModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable MainSelectable;
    public MeshRenderer[] Cards;
    public MeshRenderer[] CardBacks;
    public KMSelectable[] CardSels;
    public KMBossModule BossModule;
    public Material[] CardFronts;
    public Material CardBack;
    public Material UnlitLed;
    public Material LitLed;
    public MeshRenderer Led;

    private static int _moduleIdCounter = 1;
    private int _moduleId = -1;
    private (int one, int two)[] _swaps;
    private int[] _initialOrder;
    private int[] _finalOrder;
    private HashSet<string> _unignoredModules;
    private int _lastSolved;
    private int _curStage;
    private int _lastStage;
    private bool _isSolved = false;
    private readonly bool[] _revealed = new bool[15];

    private readonly Queue<IEnumerator> _animations = new Queue<IEnumerator>();

    private static readonly string[] _defaultIgnoreList = { "+", "14", "A>N<D", "Black Arrows", "Brainf---", "Busy Beaver", "Concentration", "Cube Synchronization", "Don't Touch Anything", "Duck Konundrum", "Floor Lights", "Forget Enigma", "Forget Any Color", "Forget Everything", "Forget Infinity", "Forget It Not", "Forget Me Not", "Forget Maze Not", "Forget Me Later", "Forget Perspective", "Forget Them All", "Forget This", "Forget The Colors", "Forget Us Not", "Gemory", "Iconic", "Keypad Directionality", "Kugelblitz", "OmegaForget", "Organization", "Out of Time", "Purgatory", "RPS Judging", "Security Council", "Shoddy Chess", "Simon Forgets", "Simon's Stages", "Soulscream", "Souvenir", "Tallordered Keys", "Tetrahedron", "The Board Walk", "The Very Annoying Button", "The Twin", "Ultimate Custom Night", "Whiteout", "Übermodule", "Bamboozling Time Keeper", "Doomsday Button", "OmegaDestroyer", "Password Destroyer", "The Time Keeper", "Timing is Everything", "Turn The Key", "Zener Cards" };

    void Start()
    {
        StartCoroutine(Initialization());

        for (var i = 0; i < CardSels.Length; i++)
            CardSels[i].OnInteract = CardPressed(i);
    }

    private KMSelectable.OnInteractHandler CardPressed(int i)
    {
        return delegate
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, CardSels[i].transform);
            CardSels[i].AddInteractionPunch(.2f);

            if (_curStage > 0 && _curStage < _lastStage)
                _animations.Enqueue(SwapCards(_swaps[_curStage - 1].one, _swaps[_curStage - 1].two));
            else if (_curStage == _lastStage && !_revealed[i])
            {
                _revealed[i] = true;
                _animations.Enqueue(FlipCard(i, _finalOrder[i]));

                for (var j = 0; j < _finalOrder[i]; j++)
                    if (!_revealed[Array.IndexOf(_finalOrder, j)])
                    {
                        Debug.Log($"[Concentration #{_moduleId}] You revealed the {_finalOrder[i] + 1} before revealing the {j + 1}. Strike!");
                        Module.HandleStrike();
                        break;
                    }

                if (_revealed.All(b => b))
                    _animations.Enqueue(Victory());
            }
            return false;
        };
    }

    private IEnumerator Victory()
    {
        yield return new WaitForSeconds(.1f);
        Debug.Log($"[Concentration #{_moduleId}] Module solved.");
        Module.HandlePass();
        _isSolved = true;
        Led.sharedMaterial = UnlitLed;
    }

    private IEnumerator Initialization()
    {
        yield return null;

        _moduleId = _moduleIdCounter++;

        var allUnignoredModules = Bomb.GetSolvableModuleNames().Except(BossModule.GetIgnoredModules(Module, _defaultIgnoreList)).ToArray();
        _lastStage = Math.Min(106, allUnignoredModules.Length);
        _unignoredModules = new HashSet<string>(allUnignoredModules);

        var allPossibleSwaps = (
            from one in Enumerable.Range(0, 15)
            from two in Enumerable.Range(one + 1, 14 - one)
            select (one, two)).ToArray().Shuffle();
        _swaps = allPossibleSwaps.Take(Math.Max(0, _lastStage - 1)).ToArray();

        _initialOrder = Enumerable.Range(0, 15).ToArray().Shuffle();
        _finalOrder = _initialOrder.ToArray();
        foreach (var (one, two) in _swaps)
            (_finalOrder[one], _finalOrder[two]) = (_finalOrder[two], _finalOrder[one]);

        for (var i = 0; i < Cards.Length; i++)
            Cards[i].sharedMaterial = CardFronts[_initialOrder[i]];

        Debug.Log($"[Concentration #{_moduleId}] There are {allUnignoredModules.Length} unignored solvable modules.");
        Debug.Log($"[Concentration #{_moduleId}] There will be {_swaps.Length} swaps.");
        Debug.Log($"[Concentration #{_moduleId}] Initial order: {_initialOrder.Select(i => i + 1).JoinString(", ")}");
        Debug.Log($"[Concentration #{_moduleId}] Swaps: {_swaps.Select(tup => $"{(char) ('A' + (tup.one % 3))}{tup.one / 3 + 1} ↔ {(char) ('A' + (tup.two % 3))}{tup.two / 3 + 1}").JoinString(", ")}");
        Debug.Log($"[Concentration #{_moduleId}] Final order: {_finalOrder.Select(i => i + 1).JoinString(", ")}");

        StartCoroutine(AnimationQueue());
        Led.sharedMaterial = _lastStage == 0 ? LitLed : UnlitLed;
    }

    private IEnumerator AnimationQueue()
    {
        while (true)
        {
            yield return null;
            if (_animations.Count == 0)
                continue;

            var anim = _animations.Dequeue();
            while (anim.MoveNext())
                yield return anim.Current;
        }
    }

    private void Update()
    {
        if (_moduleId < 0)  // module is not yet initialized
            return;

        var solved = Bomb.GetSolvedModuleNames().Where(_unignoredModules.Contains).ToArray();
        for (var i = _lastSolved; i < solved.Length && i < _lastStage; i++)
        {
            _curStage++;
            Led.sharedMaterial = _curStage == _lastStage ? LitLed : UnlitLed;

            if (_curStage == 1)
            {
                // First module solved: run the animation that flips over all cards
                _animations.Enqueue(FlipAllCards());
            }

            if (_curStage == _lastStage)
            {
                Debug.Log($"[Concentration #{_moduleId}] Last module solved. Entering solving stage.");
                break;
            }

            // Perform swap
            var (one, two) = _swaps[_curStage - 1];
            Debug.Log($"[Concentration #{_moduleId}] Stage {_curStage}: showing swap {(char) ('A' + (one % 3))}{one / 3 + 1} ↔ {(char) ('A' + (two % 3))}{two / 3 + 1}");
            _animations.Enqueue(SwapCards(one, two));
        }
        _lastSolved = solved.Length;
    }

    private Vector3 Position(int i) => new Vector3(-.036f + .036f * (i % 3), .06f - .03f * (i / 3), 0f);

    // Coroutine that runs after the first module is solved: the cards are turned face-down
    private IEnumerator FlipAllCards()
    {
        for (var i = 0; i < 15; i++)
        {
            StartCoroutine(FlipCard(i, null));
            yield return new WaitForSeconds(.19f);
        }
        yield return new WaitForSeconds(.47f);
    }

    /// <summary>
    ///     Coroutine to flip a single card.</summary>
    /// <param name="i">
    ///     Card to flip.</param>
    /// <param name="backLabel">
    ///     What should appear on the card once flipped. <c>null</c> shows the CardBack texture.</param>
    private IEnumerator FlipCard(int i, int? backLabel)
    {
        var duration = .47f;
        var elapsed = 0f;
        var basePos = Position(i);
        CardBacks[i].sharedMaterial = backLabel == null ? CardBack : CardFronts[backLabel.Value];
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            var t = elapsed / duration;
            Cards[i].transform.localEulerAngles = new Vector3(0, Mathf.Lerp(0, -180, elapsed / duration), 0);
            Cards[i].transform.localPosition = new Vector3(basePos.x, basePos.y, .1f * t * (t - 1));
        }
        Cards[i].transform.localRotation = Quaternion.identity;
        Cards[i].transform.localPosition = basePos;
        Cards[i].sharedMaterial = backLabel == null ? CardBack : CardFronts[backLabel.Value];
    }

    // Coroutine to animate the swapping of two cards while they’re face down
    private IEnumerator SwapCards(int i, int j)
    {
        var duration = .47f;
        var elapsed = 0f;

        var startPos = Position(i);
        var finalPos = Position(j);
        var ca1Pos = rotate(startPos + new Vector3(0, 0, -.05f), startPos, finalPos, 30);
        var ca2Pos = rotate(finalPos + new Vector3(0, 0, -.05f), startPos, finalPos, 30);
        var cb1Pos = rotate(startPos + new Vector3(0, 0, -.05f), finalPos, startPos, 30);
        var cb2Pos = rotate(finalPos + new Vector3(0, 0, -.05f), finalPos, startPos, 30);

        while (elapsed < duration)
        {
            var t = elapsed / duration;
            Cards[i].transform.localPosition = bézierPoint(startPos, ca1Pos, ca2Pos, finalPos, t);
            Cards[j].transform.localPosition = bézierPoint(finalPos, cb2Pos, cb1Pos, startPos, t);
            yield return null;
            elapsed += Time.deltaTime;
        }
        Cards[i].transform.localPosition = startPos;
        Cards[j].transform.localPosition = finalPos;
    }

    private static Vector3 bézierPoint(Vector3 start, Vector3 control1, Vector3 control2, Vector3 end, float t) =>
        Mathf.Pow(1 - t, 3) * start + 3 * Mathf.Pow(1 - t, 2) * t * control1 + 3 * (1 - t) * t * t * control2 + Mathf.Pow(t, 3) * end;

    private static Vector3 rotate(Vector3 pt, Vector3 axisStart, Vector3 axisEnd, float angle)
    {
        var x = pt.x;
        var y = pt.y;
        var z = pt.z;
        var a = axisStart.x;
        var b = axisStart.y;
        var c = axisStart.z;
        var u = axisEnd.x - a;
        var v = axisEnd.y - b;
        var w = axisEnd.z - c;
        var nf = Mathf.Sqrt(u * u + v * v + w * w);
        u /= nf;
        v /= nf;
        w /= nf;
        var θ = angle * Mathf.PI / 180;
        var cosθ = Mathf.Cos(θ);
        var sinθ = Mathf.Sin(θ);

        return new Vector3(
            (a * (v * v + w * w) - u * (b * v + c * w - u * x - v * y - w * z)) * (1 - cosθ) + x * cosθ + (-c * v + b * w - w * y + v * z) * sinθ,
            (b * (u * u + w * w) - v * (a * u + c * w - u * x - v * y - w * z)) * (1 - cosθ) + y * cosθ + (c * u - a * w + w * x - u * z) * sinθ,
            (c * (u * u + v * v) - w * (a * u + b * v - u * x - v * y - w * z)) * (1 - cosθ) + z * cosθ + (-b * u + a * v - v * x + u * y) * sinθ);
    }

#pragma warning disable 414
    private string TwitchHelpMessage = @"“!{0} press A1, B3, C5 [presses the cards in that order]";
#pragma warning restore 414

    private IEnumerable<KMSelectable> ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^\s*(?:press\s+)?(.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;

        var btns = new List<KMSelectable>();
        foreach (var piece in m.Groups[1].Value.Trim().Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var pieceTr = piece.Trim();
            if (pieceTr.Length < 2)
                return null;
            var x = pieceTr[0] >= 'a' && pieceTr[0] <= 'c' ? pieceTr[0] - 'a' : pieceTr[0] >= 'A' && pieceTr[0] <= 'C' ? pieceTr[0] - 'A' : -1;
            var y = pieceTr[1] >= '1' && pieceTr[1] <= '5' ? pieceTr[1] - '1' : -1;
            if (x == -1 || y == -1)
                return null;
            btns.Add(CardSels[x + 3 * y]);
        }
        return btns;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (_curStage < _lastStage)
            yield return true;

        for (var i = 0; i < 15; i++)
        {
            CardSels[Array.IndexOf(_finalOrder, i)].OnInteract();
            yield return new WaitForSeconds(.1f);
        }

        while (!_isSolved)
            yield return true;
    }
}