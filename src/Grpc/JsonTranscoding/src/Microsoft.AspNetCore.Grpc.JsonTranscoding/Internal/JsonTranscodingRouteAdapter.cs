// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Grpc.JsonTranscoding.Internal;

internal class JsonTranscodingRouteAdapter
{
    public static JsonTranscodingRouteAdapter Parse(HttpRoutePattern pattern)
    {
        var rewriteActions = new List<Action<HttpContext>>();

        var tempSegments = pattern.Segments.ToList();
        var i = 0;
        while (i < tempSegments.Count)
        {
            var segmentVariable = GetVariable(pattern, i);
            if (segmentVariable != null)
            {
                var segmentVariableEnd = ResolveEnd(pattern, segmentVariable);
                var fullPath = string.Join(".", segmentVariable.FieldPath);

                var segmentCount = segmentVariableEnd - segmentVariable.StartSegment;
                if (segmentCount == 1)
                {
                    tempSegments[i] = segmentVariable.HasCatchAllPath
                        ? $"{{**{fullPath}}}"
                        : $"{{{fullPath}}}";
                    i++;
                }
                else
                {
                    var routeParameterParts = new List<string>();
                    var routeValueFormatTemplateParts = new List<string>();
                    var variableParts = new List<string>();
                    var haveCatchAll = false;
                    var catchAllSuffix = string.Empty;

                    while (i < segmentVariableEnd && !haveCatchAll)
                    {
                        var segment = tempSegments[i];
                        var segmentType = GetSegmentType(segment);
                        switch (segmentType)
                        {
                            case SegmentType.Literal:
                                routeValueFormatTemplateParts.Add(segment);
                                break;
                            case SegmentType.Any:
                                {
                                    var parameterName = $"__Complex_{fullPath}_{i}";
                                    tempSegments[i] = $"{{{parameterName}}}";

                                    routeValueFormatTemplateParts.Add($"{{{variableParts.Count}}}");
                                    variableParts.Add(parameterName);
                                    break;
                                }
                            case SegmentType.CatchAll:
                                {
                                    var parameterName = $"__Complex_{fullPath}_{i}";
                                    var suffix = string.Join("/", tempSegments.Skip(i + 1));
                                    catchAllSuffix = string.Join("/", tempSegments.Skip(i + segmentCount - 1));
                                    var constraint = suffix.Length > 0 ? $":regex({suffix}$)" : string.Empty;
                                    tempSegments[i] = $"{{**{parameterName}{constraint}}}";

                                    routeValueFormatTemplateParts.Add($"{{{variableParts.Count}}}");
                                    variableParts.Add(parameterName);
                                    haveCatchAll = true;
                                    while (i < tempSegments.Count - 1)
                                    {
                                        tempSegments.RemoveAt(tempSegments.Count - 1);
                                    }
                                    break;
                                }
                        }
                        i++;
                    }

                    var routeValueFormatTemplate = string.Join("/", routeValueFormatTemplateParts);
                    rewriteActions.Add(context =>
                    {
                        var values = new object?[variableParts.Count];
                        for (var i = 0; i < values.Length; i++)
                        {
                            values[i] = context.Request.RouteValues[variableParts[i]];
                        }
                        var finalValue = string.Format(CultureInfo.InvariantCulture, routeValueFormatTemplate, values);

                        // Catch-all route parameter is always the last parameter. The original HTTP pattern
                        if (!string.IsNullOrEmpty(catchAllSuffix))
                        {
                            finalValue = finalValue.Substring(0, finalValue.Length - catchAllSuffix.Length - 1); // Trim "/{suffix}"
                        }
                        context.Request.RouteValues[fullPath] = finalValue;
                    });
                }
            }
            else
            {
                var segmentType = GetSegmentType(tempSegments[i]);
                switch (segmentType)
                {
                    case SegmentType.Literal:
                        // Literal is unchanged.
                        break;
                    case SegmentType.Any:
                        // Ignore any segment value.
                        tempSegments[i] = $"{{__Discard_{i}}}";
                        break;
                    case SegmentType.CatchAll:
                        // Ignore remaining segment values.
                        tempSegments[i] = $"{{**__Discard_{i}}}";
                        break;
                }

                i++;
            }
        }

        return new JsonTranscodingRouteAdapter("/" + string.Join("/", tempSegments), rewriteActions);
    }

    private static SegmentType GetSegmentType(string segment)
    {
        if (segment.StartsWith("**", StringComparison.Ordinal))
        {
            return SegmentType.CatchAll;
        }
        else if (segment.StartsWith("*", StringComparison.Ordinal))
        {
            return SegmentType.Any;
        }
        else
        {
            return SegmentType.Literal;
        }
    }

    private enum SegmentType
    {
        Literal,
        Any,
        CatchAll
    }

    private static HttpRouteVariable? GetVariable(HttpRoutePattern pattern, int i)
    {
        foreach (var variable in pattern.Variables)
        {
            var resolvedEnd = ResolveEnd(pattern, variable);

            if (i >= variable.StartSegment && i < resolvedEnd)
            {
                return variable;
            }
        }

        return null;
    }

    private static int ResolveEnd(HttpRoutePattern pattern, HttpRouteVariable variable)
    {
        int resolvedEnd;
        if (variable.EndSegment >= 0)
        {
            resolvedEnd = variable.EndSegment;
        }
        else
        {
            // Catch-all route has a negative end based on the number of segments to the end.
            var segmentsAfter = pattern.Segments.Count - variable.StartSegment;
            var length = segmentsAfter + variable.EndSegment + 1;
            resolvedEnd = variable.StartSegment + length;
        }

        return resolvedEnd;
    }

    public string ResolvedRouteTemplate { get; }
    public List<Action<HttpContext>> RewriteVariableActions { get; }

    private JsonTranscodingRouteAdapter(string resolvedRoutePattern, List<Action<HttpContext>> rewriteVariableActions)
    {
        ResolvedRouteTemplate = resolvedRoutePattern;
        RewriteVariableActions = rewriteVariableActions;
    }
}
