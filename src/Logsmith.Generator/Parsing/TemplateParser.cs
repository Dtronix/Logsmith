using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Logsmith.Generator.Diagnostics;
using Logsmith.Generator.Models;

namespace Logsmith.Generator.Parsing;

internal static class TemplateParser
{
    internal static IReadOnlyList<TemplatePart> Parse(string template)
    {
        var parts = new List<TemplatePart>();
        int pos = 0;

        while (pos < template.Length)
        {
            int braceStart = template.IndexOf('{', pos);
            if (braceStart < 0)
            {
                // Rest is literal
                parts.Add(new TemplatePart(false, template.Substring(pos)));
                break;
            }

            // Literal before placeholder
            if (braceStart > pos)
            {
                parts.Add(new TemplatePart(false, template.Substring(pos, braceStart - pos)));
            }

            int braceEnd = template.IndexOf('}', braceStart + 1);
            if (braceEnd < 0)
            {
                // Unclosed brace — treat rest as literal
                parts.Add(new TemplatePart(false, template.Substring(braceStart)));
                break;
            }

            string placeholderName = template.Substring(braceStart + 1, braceEnd - braceStart - 1);
            parts.Add(new TemplatePart(true, placeholderName));
            pos = braceEnd + 1;
        }

        return parts;
    }

    internal static IReadOnlyList<Diagnostic> Bind(
        IReadOnlyList<TemplatePart> parts,
        IReadOnlyList<ParameterInfo> parameters,
        Location location)
    {
        var diagnostics = new List<Diagnostic>();
        var boundParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Build lookup of MessageParam parameters
        var messageParams = new Dictionary<string, ParameterInfo>(StringComparer.OrdinalIgnoreCase);
        var callerParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in parameters)
        {
            if (p.Kind == ParameterKind.MessageParam)
                messageParams[p.Name] = p;
            else if (p.Kind == ParameterKind.CallerFile || p.Kind == ParameterKind.CallerLine || p.Kind == ParameterKind.CallerMember)
                callerParams.Add(p.Name);
        }

        // Bind placeholders
        foreach (var part in parts)
        {
            if (!part.IsPlaceholder)
                continue;

            // LSMITH005: caller param name used as placeholder
            if (callerParams.Contains(part.Text))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.LSMITH005,
                    location,
                    part.Text));
            }

            if (messageParams.TryGetValue(part.Text, out var param))
            {
                part.BoundParameter = param;
                boundParams.Add(param.Name);
            }
            else
            {
                // LSMITH001: no matching param
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.LSMITH001,
                    location,
                    part.Text));
            }
        }

        // LSMITH002: unreferenced message params
        foreach (var p in parameters)
        {
            if (p.Kind == ParameterKind.MessageParam && !boundParams.Contains(p.Name))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.LSMITH002,
                    location,
                    p.Name));
            }
        }

        return diagnostics;
    }

    internal static IReadOnlyList<TemplatePart> GenerateTemplateFree(
        string methodName,
        IReadOnlyList<ParameterInfo> messageParams)
    {
        var parts = new List<TemplatePart>();

        // "MethodName"
        parts.Add(new TemplatePart(false, methodName));

        for (int i = 0; i < messageParams.Count; i++)
        {
            var p = messageParams[i];
            // " param={param}"
            parts.Add(new TemplatePart(false, " " + p.Name + "="));
            var placeholder = new TemplatePart(true, p.Name) { BoundParameter = p };
            parts.Add(placeholder);
        }

        return parts;
    }
}
