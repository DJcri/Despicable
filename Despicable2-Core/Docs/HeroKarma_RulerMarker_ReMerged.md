# Hero Karma / Ideology Standing ruler diamond re-merged

This patch re-applies the `D2BandRuler` white-diamond marker fix on top of the latest manual lovin branch.

Reason:
- A later source patch bundle unintentionally restored an older `D2BandRuler.cs`.
- That older version drew the marker diamond via `GUIUtility.RotateAroundPivot`, which drifted under window movement.

What is restored:
- direct diamond-mask rendering in `D2BandRuler`
- no GUI matrix rotation for the white marker diamond

This is a source-only re-merge and still requires rebuilding the core DLL.
