# Monocraft

[Monocraft](https://github.com/IdreesInc/Monocraft) is the project's default TextMeshPro font. The original TTF and its SIL Open Font License are included alongside this file.

The committed TMP asset includes both cases of the complete Russian and English alphabets (including Ё/ё), digits, printable ASCII, Russian quotation marks, dashes, ellipsis and ₽. Its static 512×512 bitmap atlas uses point filtering and no mipmaps to preserve the pixel shapes.

New TMP text uses Monocraft automatically. For existing text, assign `Resources/Fonts & Materials/Monocraft.asset` to its Font Asset field. The default size is 36; use sizes divisible by 9 (18, 27, 36, 45, …), integer positioning and consistent canvas scaling for crisp pixels. The original `Monocraft.ttf` is also available for legacy Unity text.

`Tools → CatWalk → Fonts → Configure Monocraft` creates the atlas if absent and assigns the TMP default. `Validate Monocraft` checks the baked characters and rendering settings without relying on fallback fonts.
