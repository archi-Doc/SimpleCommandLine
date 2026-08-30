// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public enum SampleEnum
{
    Alpha = 0,
    Bravo = 1,
    Charlie = 2,
}

public class AllTypeOptions
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
    public bool Bool { get; set; }

    [SimpleOption("float")]
    public float Float { get; set; }

    [SimpleOption("double")]
    public double Double { get; set; }

    [SimpleOption("decimal")]
    public decimal Decimal { get; set; }

    [SimpleOption("char")]
    public char Char { get; set; } = '@';

    [SimpleOption("string")]
    public string String { get; set; } = string.Empty;

    [SimpleOption("enum")]
    public SampleEnum Enum { get; set; }
}

[SimpleCommand("all-type")]
public class AllTypeCommand : ISimpleCommand<AllTypeOptions>
{
    public Task Execute(AllTypeOptions option, string[] args, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public class TypeConversionTest
{
    private static readonly SimpleParserOptions Options = SimpleParserOptions.Standard with
    {
        SuppressConsoleOutput = true,
        ReadCommandFromEnvironment = false,
    };

    [Fact]
    public void AllTypesTest()
    {
        SimpleParser.TryParseOptions<AllTypeOptions>(
            "-sbyte -12 -byte 34 -short -1234 -ushort 5678 -int -90000 -uint 90000 " +
            "-long -1234567890123 -ulong 1234567890123 -bool true -float 1.5 -double -2.25 " +
            "-decimal 3.75 -char x -string \"a b\" -enum Charlie",
            out var o).IsTrue();

        o!.SByte.Is((sbyte)-12);
        o.Byte.Is((byte)34);
        o.Short.Is((short)-1234);
        o.UShort.Is((ushort)5678);
        o.Int.Is(-90000);
        o.UInt.Is(90000u);
        o.Long.Is(-1234567890123L);
        o.ULong.Is(1234567890123UL);
        o.Bool.IsTrue();
        o.Float.Is(1.5f);
        o.Double.Is(-2.25);
        o.Decimal.Is(3.75m);
        o.Char.Is('x');
        o.String.Is("a b");
        o.Enum.Is(SampleEnum.Charlie);
    }

    [Fact]
    public void NegativeNumberTest()
    {
        // A negative number is a value, not an option name.
        SimpleParser.TryParseOptions<AllTypeOptions>("-int -5 -double -2.5 -float -.25 -string abc", out var o).IsTrue();
        o!.Int.Is(-5);
        o.Double.Is(-2.5);
        o.Float.Is(-0.25f);
        o.String.Is("abc");

        // '-' and '--' are still option names.
        SimpleParserHelper.IsOptionName("-int").IsTrue();
        SimpleParserHelper.IsOptionName("--int").IsTrue();
        SimpleParserHelper.IsOptionName("-").IsTrue();
        SimpleParserHelper.IsOptionName("-1").IsFalse();
        SimpleParserHelper.IsOptionName("-.5").IsFalse();
        SimpleParserHelper.IsOptionName("abc").IsFalse();
        SimpleParserHelper.IsOptionName(string.Empty).IsFalse();
    }

    [Theory]
    [InlineData("-enum bravo", SampleEnum.Bravo)] // Case insensitive
    [InlineData("-enum BRAVO", SampleEnum.Bravo)]
    [InlineData("-enum 2", SampleEnum.Charlie)] // Numeric value
    public void EnumTest(string args, SampleEnum expected)
    {
        SimpleParser.TryParseOptions<AllTypeOptions>(args, out var o).IsTrue();
        o!.Enum.Is(expected);
    }

    [Theory]
    [InlineData("-int abc")]
    [InlineData("-int 99999999999999999999")] // Overflow
    [InlineData("-byte -1")] // Out of range
    [InlineData("-bool yes")]
    [InlineData("-enum Delta")] // Undefined member
    [InlineData("-double xyz")]
    public void InvalidValueTest(string args)
    {
        // An invalid value is an error but never throws.
        var parser = new SimpleParser([typeof(AllTypeCommand)], Options);
        parser.Parse($"all-type {args}").IsFalse();
        parser.CurrentCommand.IsNull();
    }

    [Fact]
    public void MissingValueTest()
    {
        var parser = new SimpleParser([typeof(AllTypeCommand)], Options);

        // The option is the last argument.
        parser.Parse("all-type -int").IsFalse();

        // The next argument is another option.
        parser.Parse("all-type -int -bool true").IsFalse();
    }

    [Fact]
    public void InvalidOptionMemberTest()
    {
        // A getter-only property without a backing field cannot be set.
        Assert.Throws<InvalidOperationException>(() => new SimpleParser([typeof(GetterOnlyCommand)], Options));
    }

    private class GetterOnlyOptions
    {
        [SimpleOption("value")]
        public int Value => 1;
    }

    [SimpleCommand("getter-only")]
    private class GetterOnlyCommand : ISimpleCommand<GetterOnlyOptions>
    {
        public Task Execute(GetterOnlyOptions option, string[] args, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
