﻿namespace Astro8;

public class Character
{
    public static IReadOnlyDictionary<char, int> CharToInt { get; }

    public static IReadOnlyDictionary<int, char> IntToChar { get; }

    static Character()
    {
        CharToInt = new Dictionary<char, int>
        {
            [' '] = 0,
            ['+'] = 3,
            ['-'] = 4,
            ['*'] = 5,
            ['/'] = 6,
            ['_'] = 8,
            ['<'] = 9,
            ['>'] = 10,
            ['|'] = 11,
            ['A'] = 13,
            ['a'] = 13,
            ['B'] = 14,
            ['b'] = 14,
            ['C'] = 15,
            ['c'] = 15,
            ['D'] = 16,
            ['d'] = 16,
            ['E'] = 17,
            ['e'] = 17,
            ['F'] = 18,
            ['f'] = 18,
            ['G'] = 19,
            ['g'] = 19,
            ['H'] = 20,
            ['h'] = 20,
            ['I'] = 21,
            ['i'] = 21,
            ['J'] = 22,
            ['j'] = 22,
            ['K'] = 23,
            ['k'] = 23,
            ['L'] = 24,
            ['l'] = 24,
            ['M'] = 25,
            ['m'] = 25,
            ['N'] = 26,
            ['n'] = 26,
            ['O'] = 27,
            ['o'] = 27,
            ['P'] = 28,
            ['p'] = 28,
            ['Q'] = 29,
            ['q'] = 29,
            ['R'] = 30,
            ['r'] = 30,
            ['S'] = 31,
            ['s'] = 31,
            ['T'] = 32,
            ['t'] = 32,
            ['U'] = 33,
            ['u'] = 33,
            ['V'] = 34,
            ['v'] = 34,
            ['W'] = 35,
            ['w'] = 35,
            ['X'] = 36,
            ['x'] = 36,
            ['Y'] = 37,
            ['y'] = 37,
            ['Z'] = 38,
            ['z'] = 38,
            ['0'] = 39,
            ['1'] = 40,
            ['2'] = 41,
            ['3'] = 42,
            ['4'] = 43,
            ['5'] = 44,
            ['6'] = 45,
            ['7'] = 46,
            ['8'] = 47,
            ['9'] = 48
        };

        IntToChar = CharToInt
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.First().Key);
    }
}
