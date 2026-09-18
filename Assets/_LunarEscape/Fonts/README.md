# 三语字体

来源：[Noto CJK 官方仓库](https://github.com/notofonts/noto-cjk)。本工程使用未修改的 `NotoSansCJKsc-Regular.otf`，许可证见同目录 `OFL.txt`。源字体 SHA-256：`2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`。

`Station Multilingual SDF.asset` 是从源字体生成的显示图集，包含当前 JSON 文案的中文、拉丁字母、俄文字母及数字标点。游戏使用静态图集，不依赖 Mac 或 Windows 已安装的字体。

修改文案后，在 Unity 菜单执行 **Lunar Escape → Refresh Multilingual Font**，然后运行 PlayMode 中的 `CatalogGlyphsExistAndAllLanguagesFitTheirPanels`，检查缺字与溢出。刷新会保留既有字形和资源引用。分发游戏或工程时保留本字体的许可证。
