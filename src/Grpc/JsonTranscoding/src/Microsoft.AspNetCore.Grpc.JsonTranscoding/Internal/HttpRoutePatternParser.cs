// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Grpc.JsonTranscoding.Internal;
using static Google.Rpc.Context.AttributeContext.Types;

namespace Microsoft.AspNetCore.Grpc.JsonTranscoding.Internal;

// TODO: implement an error sink.

// HTTP Template Grammar:
// Questions:
//   - what are the constraints on LITERAL and IDENT?
//   - what is the character set for the grammar?
//
// Template = "/" | "/" Segments [ Verb ] ;
// Segments = Segment { "/" Segment } ;
// Segment  = "*" | "**" | LITERAL | Variable ;
// Variable = "{" FieldPath [ "=" Segments ] "}" ;
// FieldPath = IDENT { "." IDENT } ;
// Verb     = ":" LITERAL ;
internal class HttpRoutePatternParser
{
    private readonly string _input;

    // Token delimiter indexes
    private int _tokenStart;
    private int _tokenEnd;

    // are we in nested Segments of a variable?
    private bool _inVariable;

    private readonly List<string> _segments;
    private string? _verb;
    private readonly List<HttpRouteVariable> _variables;

    public HttpRoutePatternParser(string input)
    {
        _input = input;
        _segments = new List<string>();
        _variables = new List<HttpRouteVariable>();
    }

    public bool Parse()
    {
        if (!ParseTemplate() || !ConsumedAllInput())
        {
            return false;
        }
        PostProcessVariables();
        return true;
    }

    public List<string> segments() { return _segments; }
    public string? verb() { return _verb; }
    public List<HttpRouteVariable> variables() { return _variables; }

    // only constant path segments are allowed after '**'.
    public bool ValidateParts()
    {
        const string SingleParameterKey = "/.";
        const string AnyPartKey = "*";
        const string CatchAllPathKey = "**";

        bool hasCatchAllPath = false;
        for (var i = 0; i < _segments.Count; i++)
        {
            if (!hasCatchAllPath)
            {
                if (_segments[i] == CatchAllPathKey)
                {
                    hasCatchAllPath = true;
                }
            }
            else if (_segments[i] == SingleParameterKey ||
                       _segments[i] == AnyPartKey ||
                       _segments[i] == CatchAllPathKey)
            {
                return false;
            }
        }
        return true;
    }

    // Template = "/" Segments [ Verb ] ;
    private bool ParseTemplate()
    {
        if (!Consume('/'))
        {
            // Expected '/'
            return false;
        }
        if (!ParseSegments())
        {
            return false;
        }

        if (EnsureCurrent() && current_char() == ':')
        {
            if (!ParseVerb())
            {
                return false;
            }
        }
        return true;
    }

    // Segments = Segment { "/" Segment } ;
    bool ParseSegments()
    {
        if (!ParseSegment())
        {
            return false;
        }

        for (; ; )
        {
            if (!Consume('/'))
            {
                break;
            }
            if (!ParseSegment())
            {
                return false;
            }
        }

        return true;
    }

    // Segment  = "*" | "**" | LITERAL | Variable ;
    bool ParseSegment()
    {
        if (!EnsureCurrent())
        {
            return false;
        }
        switch (current_char())
        {
            case '*':
                {
                    Consume('*');
                    if (Consume('*'))
                    {
                        // **
                        _segments.Add("**");
                        if (_inVariable)
                        {
                            return MarkVariableHasWildardPath();
                        }
                        return true;
                    }
                    else
                    {
                        _segments.Add("*");
                        return true;
                    }
                }

            case '{':
                return ParseVariable();
            default:
                return ParseLiteralSegment();
        }
    }

    // Variable = "{" FieldPath [ "=" Segments ] "}" ;
    bool ParseVariable()
    {
        if (!Consume('{'))
        {
            return false;
        }
        if (!StartVariable())
        {
            return false;
        }
        if (!ParseFieldPath())
        {
            return false;
        }
        if (Consume('='))
        {
            if (!ParseSegments())
            {
                return false;
            }
        }
        else
        {
            // {field_path} is equivalent to {field_path=*}
            _segments.Add("*");
        }
        if (!EndVariable())
        {
            return false;
        }
        if (!Consume('}'))
        {
            return false;
        }
        return true;
    }

    bool ParseLiteralSegment()
    {
        if (!TryParseLiteral(out var ls))
        {
            return false;
        }
        _segments.Add(ls);
        return true;
    }

    // FieldPath = IDENT { "." IDENT } ;
    bool ParseFieldPath()
    {
        if (!ParseIdentifier())
        {
            return false;
        }
        while (Consume('.'))
        {
            if (!ParseIdentifier())
            {
                return false;
            }
        }
        return true;
    }

    // Verb     = ":" LITERAL ;
    bool ParseVerb()
    {
        if (!Consume(':'))
        {
            return false;
        }
        if (!TryParseLiteral(out _verb))
        {
            return false;
        }
        return true;
    }

    bool ParseIdentifier()
    {
        var identifier = string.Empty;

        // Initialize to false to handle empty literal.
        var result = false;

        while (NextChar())
        {
            var c = current_char();
            switch (c)
            {
                case '.':
                case '}':
                case '=':
                    return result && AddFieldIdentifier(identifier);
                default:
                    Consume(c);
                    identifier += c;
                    break;
            }
            result = true;
        }
        return result && AddFieldIdentifier(identifier);
    }

    bool TryParseLiteral([NotNullWhen(true)] out string? lit)
    {
        lit = null;

        if (!EnsureCurrent())
        {
            return false;
        }

        // Initialize to false in case we encounter an empty literal.
        bool result = false;

        for (; ; )
        {
            var c = current_char();
            switch (c)
            {
                case '/':
                case ':':
                case '}':
                    return result;
                default:
                    Consume(c);
                    lit += c;
                    break;
            }

            result = true;

            if (!NextChar())
            {
                break;
            }
        }

        return result;
    }

    bool Consume(char? c)
    {
        if (_tokenStart >= _tokenEnd && !NextChar())
        {
            return false;
        }
        if (current_char() != c)
        {
            return false;
        }
        _tokenStart++;
        return true;
    }

    bool ConsumedAllInput() { return _tokenStart >= _input.Length; }

    bool EnsureCurrent() { return _tokenStart < _tokenEnd || NextChar(); }

    bool NextChar()
    {
        if (_tokenEnd < _input.Length)
        {
            _tokenEnd++;
            return true;
        }
        else
        {
            return false;
        }
    }

    // Returns the character looked at.
    private char? current_char() {
        return _tokenStart < _tokenEnd && _tokenEnd <= _input.Length ? _input[_tokenEnd - 1] : null;
    }

    private HttpRouteVariable CurrentVariable() { return _variables.Last(); }

    bool StartVariable()
    {
        if (!_inVariable)
        {
            _variables.Add(new HttpRouteVariable());
            CurrentVariable().StartSegment = _segments.Count;
            CurrentVariable().HasCatchAllPath = false;
            _inVariable = true;
            return true;
        }
        else
        {
            // nested variables are not allowed
            return false;
        }
    }

    bool EndVariable()
    {
        if (_inVariable && _variables.Any())
        {
            CurrentVariable().EndSegment = _segments.Count;
            _inVariable = false;
            return ValidateVariable(CurrentVariable());
        }
        else
        {
            // something's wrong we're not in a variable
            return false;
        }
    }

    bool AddFieldIdentifier(string id)
    {
        if (_inVariable && _variables.Any())
        {
            CurrentVariable().FieldPath.Add(id);
            return true;
        }
        else
        {
            // something's wrong we're not in a variable
            return false;
        }
    }

    bool MarkVariableHasWildardPath()
    {
        if (_inVariable && _variables.Any())
        {
            CurrentVariable().HasCatchAllPath = true;
            return true;
        }
        else
        {
            // something's wrong we're not in a variable
            return false;
        }
    }

    bool ValidateVariable(HttpRouteVariable var)
    {
        return var.FieldPath.Any() && (var.StartSegment < var.EndSegment) &&
               (var.EndSegment <= _segments.Count);
    }

    void PostProcessVariables()
    {
        foreach (var item in _variables)
        {
            if (item.HasCatchAllPath)
            {
                // if the variable contains a '**', store the end_positon
                // relative to the end, such that -1 corresponds to the end
                // of the path. As we only support fixed path after '**',
                // this will allow the matcher code to reconstruct the variable
                // value based on the url segments.
                item.EndSegment = (item.EndSegment - _segments.Count - 1);
            }
        }
    }
}

internal class HttpRoutePattern
{
    private HttpRoutePattern(List<string> segments, string? verb, List<HttpRouteVariable> variables)
    {
        _segments = segments;
        _verb = verb;
        _variables = variables;
    }
    private readonly List<string> _segments;
    private readonly string? _verb;
    private readonly List<HttpRouteVariable> _variables;

    public static HttpRoutePattern? Parse(string ht)
    {
        if (ht == "/")
        {
            return new HttpRoutePattern(new List<string>(), string.Empty, new List<HttpRouteVariable>());
        }

        HttpRoutePatternParser p = new HttpRoutePatternParser(ht);
        if (!p.Parse() || !p.ValidateParts())
        {
            return null;
        }

        return new HttpRoutePattern(p.segments(), p.verb(), p.variables());
    }

    public List<string> Segments => _segments;
    public string? Verb => _verb;
    public List<HttpRouteVariable> Variables => _variables;
}

// The info about a variable binding {variable=subpath} in the template.
public class HttpRouteVariable
{
    public int Index;

    // Specifies the range of segments [start_segment, end_segment) the
    // variable binds to. Both start_segment and end_segment are 0 based.
    // end_segment can also be negative, which means that the position is
    // specified relative to the end such that -1 corresponds to the end
    // of the path.
    public int StartSegment;
    public int EndSegment;

    // The path of the protobuf field the variable binds to.
    public List<string> FieldPath = new List<string>();

    // Do we have a ** in the variable template?
    public bool HasCatchAllPath;
}
