using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Despicable.HeroKarma.Patches.HeroKarma;

internal static class HKPatchTargetUtil
{
    public static IEnumerable<MethodBase> FindFirstMethods(string[] typeNames, string methodName)
    {
        if (typeNames == null || typeNames.Length == 0 || string.IsNullOrEmpty(methodName))
            yield break;

        HashSet<MethodBase> seen = new();
        for (int i = 0; i < typeNames.Length; i++)
        {
            Type type = AccessTools.TypeByName(typeNames[i]);
            if (type == null)
                continue;

            MethodInfo method = AccessTools.Method(type, methodName);
            if (method != null && seen.Add(method))
                yield return method;
        }
    }

    public static IEnumerable<MethodBase> FindDeclaredMethods(Type type, Func<MethodInfo, bool> predicate)
    {
        if (type == null || predicate == null)
            yield break;

        HashSet<MethodBase> seen = new();
        List<MethodInfo> methods = AccessTools.GetDeclaredMethods(type);
        if (methods == null)
            yield break;

        for (int i = 0; i < methods.Count; i++)
        {
            MethodInfo method = methods[i];
            if (method == null || !predicate(method))
                continue;

            if (seen.Add(method))
                yield return method;
        }
    }

    public static IEnumerable<MethodBase> FindDeclaredMethods(Func<Type, bool> typePredicate, Func<MethodInfo, bool> predicate)
    {
        if (typePredicate == null || predicate == null)
            yield break;

        HashSet<MethodBase> seen = new();
        foreach (Type type in AccessTools.AllTypes())
        {
            if (!typePredicate(type))
                continue;

            List<MethodInfo> methods = AccessTools.GetDeclaredMethods(type);
            if (methods == null)
                continue;

            for (int i = 0; i < methods.Count; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || !predicate(method))
                    continue;

                if (seen.Add(method))
                    yield return method;
            }
        }
    }

    public static IEnumerable<MethodBase> FindMethods(Type type, BindingFlags flags, Func<MethodInfo, bool> predicate)
    {
        if (type == null || predicate == null)
            yield break;

        HashSet<MethodBase> seen = new();
        MethodInfo[] methods = type.GetMethods(flags);
        if (methods == null || methods.Length == 0)
            yield break;

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method == null || !predicate(method))
                continue;

            if (seen.Add(method))
                yield return method;
        }
    }
}
