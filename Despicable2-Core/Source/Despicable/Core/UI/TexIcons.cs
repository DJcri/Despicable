using UnityEngine;
using Verse;

namespace Despicable;
[StaticConstructorOnStartup]
public static class TexIcons
{
    // [-= Hero Module =-]
    public static readonly Texture2D HeroGizmo = ContentFinder<Texture2D>.Get("UI/Karma/HeroGizmo");

    // Good karma
    public static readonly Texture2D Proselytize = ContentFinder<Texture2D>.Get("UI/Abilities/Proselytize");
    public static readonly Texture2D PetAnimal = ContentFinder<Texture2D>.Get("UI/Abilities/PetAnimal");
    public static readonly Texture2D PepTalk = ContentFinder<Texture2D>.Get("UI/Abilities/PepTalk");
    public static readonly Texture2D Uplift = ContentFinder<Texture2D>.Get("UI/Abilities/Uplift");

    // Bad karma
    public static readonly Texture2D OperantTraining = ContentFinder<Texture2D>.Get("UI/Abilities/OperantTraining");
    public static readonly Texture2D Oversee = ContentFinder<Texture2D>.Get("UI/Abilities/Oversee");
    public static readonly Texture2D Torture = ContentFinder<Texture2D>.Get("UI/Abilities/Torture");
    public static readonly Texture2D FearMonger = ContentFinder<Texture2D>.Get("UI/Abilities/FearMonger");
}
