using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;

internal static class FacePartRuntimeGraphicCache
{
    private sealed class CachedGraphicEntry
    {
        public Graphic Graphic;
        public Material[] OwnedMaterials;
    }

    private static readonly Dictionary<string, CachedGraphicEntry> CachedGraphicsByKey = new(StringComparer.Ordinal);

    public static void ResetRuntimeState()
    {
        foreach (CachedGraphicEntry entry in CachedGraphicsByKey.Values)
        {
            if (entry?.OwnedMaterials == null)
                continue;

            for (int i = 0; i < entry.OwnedMaterials.Length; i++)
            {
                Material material = entry.OwnedMaterials[i];
                if (material != null)
                    UnityEngine.Object.Destroy(material);
            }
        }

        CachedGraphicsByKey.Clear();
    }

    public static Graphic GetOrCreate(string cacheKey, Func<(Graphic graphic, Material[] ownedMaterials)> factory)
    {
        if (cacheKey.NullOrEmpty() || factory == null)
            return null;

        if (CachedGraphicsByKey.TryGetValue(cacheKey, out CachedGraphicEntry cached) && cached?.Graphic != null)
            return cached.Graphic;

        (Graphic graphic, Material[] ownedMaterials) created = factory();
        if (created.graphic == null)
            return null;

        CachedGraphicsByKey[cacheKey] = new CachedGraphicEntry
        {
            Graphic = created.graphic,
            OwnedMaterials = created.ownedMaterials,
        };
        return created.graphic;
    }
}
