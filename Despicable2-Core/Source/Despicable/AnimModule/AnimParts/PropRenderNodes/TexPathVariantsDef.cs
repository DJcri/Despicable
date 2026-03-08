using System.Collections.Generic;
using Verse;

namespace Despicable;
/// <summary>
/// Legacy texture variant list for props.
///
/// 2B migration path:
/// - Prefer named states via <see cref="stateIds"/> (matching the native 1.6 "graphic states" concept).
/// - If <see cref="stateIds"/> is omitted, indices map to "variant_1", "variant_2", ...
/// </summary>
public class TexPathVariantsDef : Def
{
    public List<string> variants;

    // Optional, must match variants.Count if provided.
    public List<string> stateIds;
}
