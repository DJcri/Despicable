# Gender Works runtime integration patch notes

This bundle patches the source and assets for the Gender Works runtime/rendering regression.

Implemented changes:
- Gender Works anatomy is now treated as logical runtime truth even when `D2_ExternalGenitals` is not present yet.
- Runtime stage tags (`repro_male`, `repro_female`) now fall back more safely so stage assignment does not fail open into no-animation states.
- Gender Works resolution no longer treats a callable reflection method as a positive anatomy result by itself.
- Anatomy resync now triggers immediately on health changes instead of waiting for a later rare tick.
- Lovin render nodes now support both penis and vagina defs.
- Added a `Genital_Vagina` def and placeholder runtime textures.
- Added a def injector that equips all humanlike ThingDefs / bodies with the required anatomy comps and `D2_ExternalGenitals` body part when missing.
- Repro compatibility helpers now key off logical anatomy rather than only the Despicable body slot.

Important:
- The container used for patching did not include a C# build toolchain, so the shipped DLL was not rebuilt here.
- Source, defs, and textures are updated and ready to compile in the included project.
