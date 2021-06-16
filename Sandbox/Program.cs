// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using SimpleCommandLine;

namespace ConsoleApp1
{
    [SimpleCommand("test")]
    public class TestCommand : ISimpleCommand
    {
        [SimpleOption("number", "n")]
        public int N { get; set; } = 10;

        public void Run()
        {
            Console.WriteLine("Test command");
            Console.WriteLine($"N is {this.N}");
        }

        public void Run(string[] args)
        {
            Console.WriteLine("Test command");
            Console.WriteLine($"N is {this.N}");
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var commandTypes = new Type[]
            {
                typeof(TestCommand),
            };

            var commands = new ISimpleCommand[]
            {
                new TestCommand(),
            };

            SimpleParser.ParseAndRun(commandTypes, "-help     -version  a　b     c   d e");

            /*var p = SimpleParser.Parse(commandTypes, args);
            p.Run();
            p.ShowHelp();*/
        }
    }
}
