// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace SimpleCommandLine;

internal interface ISimpleParser
{
    public void AddErrorMessage(string message);

    public void TryAddOptionClassUsage(SimpleParser.OptionClass optionClass);

    public SimpleParserOptions ParserOptions { get; }
}
