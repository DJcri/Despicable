using System;
using System.Reflection;
using Verse;

namespace Despicable;
/// <summary>
/// Shared pawn render-node helpers used by multiple modules.
/// </summary>
public static partial class CommonUtil
{
    public static PawnRenderNodeProperties CloneNodeProperties(PawnRenderNodeProperties source)
    {
        if (source == null)
        {
            return null;
        }

        Type currentType = source.GetType();
        PawnRenderNodeProperties clone = (PawnRenderNodeProperties)Activator.CreateInstance(currentType);

        while (currentType != null)
        {
            foreach (FieldInfo field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                field.SetValue(clone, field.GetValue(source));
            }

            currentType = currentType.BaseType;
        }

        return clone;
    }

    /// <summary>
    /// Creates a node from source/shared properties by cloning them first.
    /// Use this path for Def-owned templates or any other shared properties instance.
    /// </summary>
    public static PawnRenderNode CreateNode(Pawn pawn, PawnRenderNodeProperties props, PawnRenderNodeTagDef parentTag = null)
    {
        PawnRenderNodeProperties nodeProps = CloneNodeProperties(props);
        return CreateNodeFromOwnedProps(pawn, nodeProps, parentTag);
    }

    /// <summary>
    /// Creates a node from already-owned mutable properties.
    /// Callers must only pass a properties instance they can safely mutate.
    /// </summary>
    public static PawnRenderNode CreateNodeFromOwnedProps(Pawn pawn, PawnRenderNodeProperties props, PawnRenderNodeTagDef parentTag = null)
    {
        if (props == null)
        {
            return null;
        }

        if (parentTag != null)
        {
            props.parentTagDef = parentTag;
        }

        return (PawnRenderNode)Activator.CreateInstance(props.nodeClass, new object[]
        {
            pawn,
            props,
            pawn.Drawer.renderer.renderTree
        });
    }
}
