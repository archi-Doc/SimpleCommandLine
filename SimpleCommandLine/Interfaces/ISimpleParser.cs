// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace SimpleCommandLine;

internal interface ISimpleParser
{
    void AddErrorMessage(string message);

    void TryAddOptionClassUsage(SimpleParser.OptionClass optionClass);

    SimpleParserOptions ParserOptions { get; }
}
