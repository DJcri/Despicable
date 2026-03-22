using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Despicable;

/// <summary>
/// When Despicable owns supported eye-color genes, suppress foreign render nodes sourced from
/// those same genes so vanilla/external overlays cannot draw on top of the Despicable eye area.
/// </summary>
internal static class HarmonyPatch_ForeignEyeGeneGraphics
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Dictionary<Type, MemberInfo[]> CandidateMembersByType = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PawnRenderNode, NodeMatchCache> NodeMatchCacheByNode = new();
    private static readonly IEqualityComparer<object> ReferenceComparer = new ReferenceIdentityComparer();

    private static readonly string[] PreferredMemberNames =
    {
        "gene",
        "sourceGene",
        "geneDef",
        "sourceGeneDef",
        "graphicData",
        "geneGraphicData",
        "graphicRecord",
        "geneGraphicRecord",
        "record",
        "props",
        "Props"
    };

    public static bool ShouldSuppressNode(PawnRenderNode node, Pawn pawn)
    {
        if (ModMain.IsNlFacialInstalled || node == null || pawn == null)
            return false;

        if (pawn.RaceProps?.Humanlike != true)
            return false;

        if (node is PawnRenderNode_EyeAddon)
            return false;

        string nodeNamespace = node.GetType().Namespace ?? string.Empty;
        if (nodeNamespace.StartsWith("Despicable", StringComparison.Ordinal))
            return false;

        CompFaceParts compFaceParts = FacePartRenderNodeContextCache.ResolveCompFaceParts(pawn);
        if (compFaceParts?.IsRenderActiveNow() != true)
            return false;

        if (compFaceParts.ShouldSuppressForeignEyeGeneGraphicsThisTick() != true)
            return false;

        return MatchesSupportedEyeGeneNode(node);
    }

    internal static bool MatchesSupportedEyeGeneNode(PawnRenderNode node)
    {
        if (node == null)
            return false;

        NodeMatchCache cache = NodeMatchCacheByNode.GetValue(node, CreateNodeMatchCache);
        return cache.MatchesSupportedEyeGene;
    }

    private static NodeMatchCache CreateNodeMatchCache(PawnRenderNode node)
    {
        bool matches = TryResolveSupportedEyeGeneDefName(node, out _)
            || MatchesSupportedEyeGeneText(node?.Props?.debugLabel)
            || MatchesSupportedEyeGeneText(node?.Props?.texPath)
            || MatchesSupportedEyeGeneText(node?.GetType().Name);

        return new NodeMatchCache(matches);
    }

    private static bool TryResolveSupportedEyeGeneDefName(PawnRenderNode node, out string geneDefName)
    {
        geneDefName = null;
        HashSet<object> visited = new(ReferenceComparer);
        return TryResolveSupportedEyeGeneDefName(node, visited, 0, out geneDefName)
            || TryResolveSupportedEyeGeneDefName(node?.Props, visited, 0, out geneDefName);
    }

    private static bool TryResolveSupportedEyeGeneDefName(object source, HashSet<object> visited, int depth, out string geneDefName)
    {
        geneDefName = null;
        if (source == null || depth > 2)
            return false;

        if (!visited.Add(source))
            return false;

        switch (source)
        {
            case Gene gene when CompFaceParts.IsSupportedEyeGeneDefName(gene.def?.defName):
                geneDefName = gene.def.defName;
                return true;
            case GeneDef geneDef when CompFaceParts.IsSupportedEyeGeneDefName(geneDef.defName):
                geneDefName = geneDef.defName;
                return true;
            case string text when TryResolveSupportedEyeGeneDefNameFromText(text, out geneDefName):
                return true;
        }

        MemberInfo[] members = GetCandidateMembers(source.GetType());
        for (int i = 0; i < members.Length; i++)
        {
            object value = TryGetMemberValue(source, members[i]);
            if (value == null)
                continue;

            switch (value)
            {
                case Gene gene when CompFaceParts.IsSupportedEyeGeneDefName(gene.def?.defName):
                    geneDefName = gene.def.defName;
                    return true;
                case GeneDef geneDef when CompFaceParts.IsSupportedEyeGeneDefName(geneDef.defName):
                    geneDefName = geneDef.defName;
                    return true;
                case string text when TryResolveSupportedEyeGeneDefNameFromText(text, out geneDefName):
                    return true;
            }

            if (ShouldTraverse(value.GetType())
                && TryResolveSupportedEyeGeneDefName(value, visited, depth + 1, out geneDefName))
            {
                return true;
            }
        }

        return false;
    }

    private static MemberInfo[] GetCandidateMembers(Type type)
    {
        if (type == null)
            return Array.Empty<MemberInfo>();

        if (CandidateMembersByType.TryGetValue(type, out MemberInfo[] cached))
            return cached;

        List<MemberInfo> members = new();
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            for (int i = 0; i < PreferredMemberNames.Length; i++)
            {
                string name = PreferredMemberNames[i];
                FieldInfo field = current.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (field != null && seen.Add($"F:{field.DeclaringType?.FullName}:{field.Name}"))
                    members.Add(field);

                PropertyInfo property = current.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0 && property.CanRead
                    && seen.Add($"P:{property.DeclaringType?.FullName}:{property.Name}"))
                {
                    members.Add(property);
                }
            }

            FieldInfo[] fields = current.GetFields(Flags | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!IsCandidateMemberType(field.FieldType))
                    continue;

                if (seen.Add($"F:{field.DeclaringType?.FullName}:{field.Name}"))
                    members.Add(field);
            }

            PropertyInfo[] properties = current.GetProperties(Flags | BindingFlags.DeclaredOnly);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;

                if (!IsCandidateMemberType(property.PropertyType))
                    continue;

                if (seen.Add($"P:{property.DeclaringType?.FullName}:{property.Name}"))
                    members.Add(property);
            }
        }

        cached = members.ToArray();
        CandidateMembersByType[type] = cached;
        return cached;
    }

    private static object TryGetMemberValue(object source, MemberInfo member)
    {
        try
        {
            return member switch
            {
                FieldInfo field => field.GetValue(source),
                PropertyInfo property => property.GetValue(source, null),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool IsCandidateMemberType(Type type)
    {
        if (type == null)
            return false;

        if (type == typeof(string))
            return true;

        if (typeof(Gene).IsAssignableFrom(type) || typeof(GeneDef).IsAssignableFrom(type))
            return true;

        string name = type.Name ?? string.Empty;
        return name.IndexOf("Gene", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Graphic", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Node", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Props", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ShouldTraverse(Type type)
    {
        if (type == null || type == typeof(string))
            return false;

        if (typeof(Gene).IsAssignableFrom(type) || typeof(GeneDef).IsAssignableFrom(type))
            return true;

        string ns = type.Namespace ?? string.Empty;
        if (ns.StartsWith("RimWorld", StringComparison.Ordinal) || ns.StartsWith("Verse", StringComparison.Ordinal))
            return true;

        string name = type.Name ?? string.Empty;
        return name.IndexOf("Gene", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Graphic", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Node", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Props", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MatchesSupportedEyeGeneText(string text)
    {
        return TryResolveSupportedEyeGeneDefNameFromText(text, out _);
    }

    private static bool TryResolveSupportedEyeGeneDefNameFromText(string text, out string geneDefName)
    {
        geneDefName = null;
        if (text.NullOrEmpty())
            return false;

        if (ContainsToken(text, "Eyes_Red") || ContainsToken(text, "RedEyes") || ContainsToken(text, "Red_Eyes"))
        {
            geneDefName = "Eyes_Red";
            return true;
        }

        if (ContainsToken(text, "Eyes_Gray") || ContainsToken(text, "GrayEyes") || ContainsToken(text, "GreyEyes")
            || ContainsToken(text, "Gray_Eyes") || ContainsToken(text, "Grey_Eyes"))
        {
            geneDefName = "Eyes_Gray";
            return true;
        }

        return false;
    }

    private static bool ContainsToken(string text, string token)
    {
        return !text.NullOrEmpty()
            && !token.NullOrEmpty()
            && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }


    private sealed class NodeMatchCache
    {
        public NodeMatchCache(bool matchesSupportedEyeGene)
        {
            MatchesSupportedEyeGene = matchesSupportedEyeGene;
        }

        public bool MatchesSupportedEyeGene { get; }
    }

    private sealed class ReferenceIdentityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
