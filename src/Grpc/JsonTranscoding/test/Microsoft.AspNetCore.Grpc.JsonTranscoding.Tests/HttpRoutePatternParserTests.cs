// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Grpc.JsonTranscoding.Internal;

namespace Microsoft.AspNetCore.Grpc.JsonTranscoding.Tests;

public class HttpRoutePatternParserTests
{
    [Fact]
    public void ParseMultipleVariables()
    {
        var route = HttpRoutePattern.Parse("/shelves/{shelf}/books/{book}")!;
        Assert.Null(route.Verb);
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("shelves", s),
            s => Assert.Equal("*", s),
            s => Assert.Equal("books", s),
            s => Assert.Equal("*", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(1, v.StartSegment);
                Assert.Equal(2, v.EndSegment);
                Assert.Equal("shelf", string.Join(".", v.FieldPath));
                Assert.False(v.HasCatchAllPath);
            },
            v =>
            {
                Assert.Equal(3, v.StartSegment);
                Assert.Equal(4, v.EndSegment);
                Assert.Equal("book", string.Join(".", v.FieldPath));
                Assert.False(v.HasCatchAllPath);
            });
    }

    [Fact]
    public void ParseComplexVariable()
    {
        var route = HttpRoutePattern.Parse("/v1/{book.name=shelves/*/books/*}")!;
        Assert.Null(route.Verb);
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("v1", s),
            s => Assert.Equal("shelves", s),
            s => Assert.Equal("*", s),
            s => Assert.Equal("books", s),
            s => Assert.Equal("*", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(1, v.StartSegment);
                Assert.Equal(5, v.EndSegment);
                Assert.Equal("book.name", string.Join(".", v.FieldPath));
                Assert.False(v.HasCatchAllPath);
            });
    }

    [Fact]
    public void ParseCatchAllSegment()
    {
        var route = HttpRoutePattern.Parse("/shelves/**")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("shelves", s),
            s => Assert.Equal("**", s));
        Assert.Empty(route.Variables);
    }

    [Fact]
    public void ParseCatchAllSegment2()
    {
        var route = HttpRoutePattern.Parse("/**")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("**", s));
        Assert.Empty(route.Variables);
    }

    [Fact]
    public void ParseAnySegment()
    {
        var route = HttpRoutePattern.Parse("/*")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("*", s));
        Assert.Empty(route.Variables);
    }

    [Fact]
    public void ParseVerb()
    {
        var route = HttpRoutePattern.Parse("/a:foo")!;
        Assert.Equal("foo", route.Verb);
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("a", s));
        Assert.Empty(route.Variables);
    }

    [Fact]
    public void ParseAnyAndCatchAllSegment()
    {
        var route = HttpRoutePattern.Parse("/*/**")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("*", s),
            s => Assert.Equal("**", s));
        Assert.Empty(route.Variables);
    }

    [Fact]
    public void ParseAnyAndCatchAllSegment2()
    {
        var route = HttpRoutePattern.Parse("/*/a/**")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("*", s),
            s => Assert.Equal("a", s),
            s => Assert.Equal("**", s));
        Assert.Empty(route.Variables);
    }

    [Fact]
    public void ParseNestedFieldPath()
    {
        var route = HttpRoutePattern.Parse("/a/{a.b.c}")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("a", s),
            s => Assert.Equal("*", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(1, v.StartSegment);
                Assert.Equal(2, v.EndSegment);
                Assert.Equal("a.b.c", string.Join(".", v.FieldPath));
                Assert.False(v.HasCatchAllPath);
            });
    }

    [Fact]
    public void ParseComplexNestedFieldPath()
    {
        var route = HttpRoutePattern.Parse("/a/{a.b.c=*}")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("a", s),
            s => Assert.Equal("*", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(1, v.StartSegment);
                Assert.Equal(2, v.EndSegment);
                Assert.Equal("a.b.c", string.Join(".", v.FieldPath));
                Assert.False(v.HasCatchAllPath);
            });
    }

    [Fact]
    public void ParseComplexCatchAll()
    {
        var route = HttpRoutePattern.Parse("/a/{b=**}")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("a", s),
            s => Assert.Equal("**", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(1, v.StartSegment);
                Assert.Equal(-1, v.EndSegment);
                Assert.Equal("b", string.Join(".", v.FieldPath));
                Assert.True(v.HasCatchAllPath);
            });
    }

    [Fact]
    public void ParseComplexPrefixSegment()
    {
        var route = HttpRoutePattern.Parse("/a/{b=c/*}")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("a", s),
            s => Assert.Equal("c", s),
            s => Assert.Equal("*", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(1, v.StartSegment);
                Assert.Equal(3, v.EndSegment);
                Assert.Equal("b", string.Join(".", v.FieldPath));
                Assert.False(v.HasCatchAllPath);
            });
    }

    [Fact]
    public void ParseComplexPrefixSuffixSegment()
    {
        var route = HttpRoutePattern.Parse("/a/{b=c/*/d}")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("a", s),
            s => Assert.Equal("c", s),
            s => Assert.Equal("*", s),
            s => Assert.Equal("d", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(1, v.StartSegment);
                Assert.Equal(4, v.EndSegment);
                Assert.Equal("b", string.Join(".", v.FieldPath));
                Assert.False(v.HasCatchAllPath);
            });
    }

    [Fact]
    public void ParseComplexPathCatchAll()
    {
        var route = HttpRoutePattern.Parse("/a/{b=c/**}")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("a", s),
            s => Assert.Equal("c", s),
            s => Assert.Equal("**", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(1, v.StartSegment);
                Assert.Equal(-1, v.EndSegment);
                Assert.Equal("b", string.Join(".", v.FieldPath));
                Assert.True(v.HasCatchAllPath);
            });
    }

    [Fact]
    public void ParseComplexPrefixSuffixCatchAll()
    {
        var route = HttpRoutePattern.Parse("/{x.y.z=a/**/b}/c/d")!;
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("a", s),
            s => Assert.Equal("**", s),
            s => Assert.Equal("b", s),
            s => Assert.Equal("c", s),
            s => Assert.Equal("d", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(0, v.StartSegment);
                Assert.Equal(-3, v.EndSegment);
                Assert.Equal("x.y.z", string.Join(".", v.FieldPath));
                Assert.True(v.HasCatchAllPath);
            });
    }

    [Fact]
    public void ParseCatchAllVerb()
    {
        var route = HttpRoutePattern.Parse("/a/{b=*}/**:verb")!;
        Assert.Equal("verb", route.Verb);
        Assert.Collection(
            route.Segments,
            s => Assert.Equal("a", s),
            s => Assert.Equal("*", s),
            s => Assert.Equal("**", s));
        Assert.Collection(
            route.Variables,
            v =>
            {
                Assert.Equal(1, v.StartSegment);
                Assert.Equal(2, v.EndSegment);
                Assert.Equal("b", string.Join(".", v.FieldPath));
                Assert.False(v.HasCatchAllPath);
            });
    }

    [Theory]
    [InlineData("")]
    [InlineData("//")]
    [InlineData("/{}")]
    [InlineData("/a/")]
    [InlineData(":verb")]
    [InlineData(":")]
    [InlineData("/:")]
    [InlineData("/{var}:")]
    [InlineData("/{")]
    [InlineData("/a{x}")]
    [InlineData("/{x}a")]
    [InlineData("/{x}{y}")]
    [InlineData("/{var=a/{nested=b}}")]
    public void Error(string pattern)
    {
        var route = HttpRoutePattern.Parse(pattern);
        Assert.Null(route);
    }
}
