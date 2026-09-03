// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using Xunit;

namespace xUnitTest;

public class AssertionHelperTest
{
    [Fact]
    public void UnorderedStructuralAssertionAccountsForDuplicateValues()
    {
        new[] { 1, 2, 1 }.IsStructuralEqualIgnoreCollectionOrder(new[] { 1, 1, 2 });
        Assert.ThrowsAny<Exception>(() => new[] { 1, 2 }.IsStructuralEqualIgnoreCollectionOrder(new[] { 1, 1 }));
    }
}
