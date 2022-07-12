// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable SA1602 // Enumeration items should be documented

namespace ConsoleApp1
{
    public enum TestEnum
    {
        Yes,
        No,
        Hanbun,
    }

    public class TypeOptions
    {
        [SimpleOption("sbyte")]
        public sbyte SByte { get; set; } = -1;

        [SimpleOption("byte")]
        public byte Byte { get; set; } = 2;

        [SimpleOption("short")]
        public short Short { get; set; } = -3;

        [SimpleOption("ushort")]
        public ushort UShort { get; set; } = 4;

        [SimpleOption("int")]
        public int Int { get; set; } = -5;

        [SimpleOption("uint")]
        public uint UInt { get; set; } = 6;

        [SimpleOption("long")]
        public long Long { get; set; } = -7;

        [SimpleOption("ulong")]
        public ulong ULong { get; set; } = 8;

        [SimpleOption("bool")]
        public bool Bool { get; set; } = true;

        [SimpleOption("single")]
        public float Single { get; set; } = 9.1f;

        [SimpleOption("double")]
        public double Double { get; set; } = -10.5;

        [SimpleOption("char")]
        public char Char { get; set; } = '@';

        [SimpleOption("decimal")]
        public decimal Decimal { get; set; } = 2000;

        [SimpleOption("string")]
        public string String { get; set; } = "2021";

        [SimpleOption("enum")]
        public TestEnum Enum { get; set; } = TestEnum.No;
    }

    [SimpleCommand("type")]
    public class TypeCommand : ISimpleCommandAsync<TypeOptions>
    {
        public async Task RunAsync(TypeOptions option, string[] args)
        {
            Console.WriteLine("Test command:");
            Console.WriteLine($"SByte: {option.SByte}");
            Console.WriteLine($"Byte: {option.Byte}");
            Console.WriteLine($"Short: {option.Short}");
            Console.WriteLine($"UShort: {option.UShort}");
            Console.WriteLine($"Int: {option.Int}");
            Console.WriteLine($"UInt: {option.UInt}");
            Console.WriteLine($"Long: {option.Long}");
            Console.WriteLine($"ULong: {option.ULong}");
            Console.WriteLine($"Bool: {option.Bool}");
            Console.WriteLine($"Single: {option.Single}");
            Console.WriteLine($"Double: {option.Double}");
            Console.WriteLine($"Char: {option.Char}");
            Console.WriteLine($"Decimal: {option.Decimal}");
            Console.WriteLine($"String: {option.String}");
            Console.WriteLine($"Enum: {option.Enum}");
        }
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var commandTypes = new Type[]
            {
                typeof(TypeCommand),
            };

            await SimpleParser.ParseAndRunAsync(commandTypes, args);
        }
    }
}
