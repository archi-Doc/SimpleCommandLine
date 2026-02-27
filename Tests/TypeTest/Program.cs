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

    [SimpleCommand("string")]
    public class StringCommand : ISimpleCommandAsync<StringCommand.Options>
    {
        public class Options
        {
            [SimpleOption("s0", ArgumentProcessing = ArgumentProcessing.RemoveNewlines)]
            public string S0 { get; set; } = string.Empty;

            [SimpleOption("s1", ArgumentProcessing = ArgumentProcessing.AsIs)]
            public string S1 { get; set; } = string.Empty;

            [SimpleOption("s2", ArgumentProcessing = ArgumentProcessing.AsIs)]
            public string S2 { get; set; } = string.Empty;

            [SimpleOption("s3")]
            public string S3 { get; set; } = string.Empty;

            [SimpleOption("s4")]
            public string S4 { get; set; } = string.Empty;
        }

        public async Task RunAsync(StringCommand.Options options, string[] args)
        {
            Console.WriteLine("String command:");

            Console.WriteLine($"S0: {options.S0}");
            Console.WriteLine($"S1: {options.S1}");
            Console.WriteLine($"S2: {options.S2}");
            Console.WriteLine($"S3: {options.S3}");
            Console.WriteLine($"S4: {options.S4}");
        }
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var delimiter = "\"\"\"";

            var commandTypes = new Type[]
            {
                typeof(TypeCommand),
                typeof(StringCommand),
            };

            // await SimpleParser.ParseAndRunAsync(commandTypes, "-double 1.2345 -String abc");
            await SimpleParser.ParseAndRunAsync(commandTypes, $"string -s0 \"ab\r\nc\" -s1 'bbb' -s2 {delimiter}a\r\nb\nc{delimiter} -s3 {delimiter}a\r\nb\nc{delimiter} -s4 \\\"test\\\"");
        }
    }
}
