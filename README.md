# nekomata
_(Chinese version readme at before)_

nekomata is a translation tool designed for RPG Maker games, **supporting RPG Maker MV, MZ, VX, and VX Ace**. It provides a modern UI for managing translation projects and editing game text.

## Features

- **Project Management**: File-based project system (.nkproj).
- **Engine Support**:
  - RPG Maker MV / MZ (.json)
  - RPG Maker VX / VX Ace (.rvdata2)
- **Modern UI**: Built with WPF-UI, featuring a clean, Windows 11-style interface.
- **Translation Workflow**:
  - Import game directories.
  - Edit translations in a grid view.
  - Apply translations to generate patched game files (non-destructive).
- **Text Handling**:
  - Merges consecutive text commands for better context.
  - Supports "Copy Original Text" to clipboard.

## Usage

1. **Open/Create Project**: Launch Nekomata and create a new project or open an existing `.nkproj` file.
2. **Import Game**: Click "Import" and select the game directory.
   - **Important for VX / VX Ace**: You must unpack the game archives (e.g., `Game.rgss3a`) before importing. Nekomata requires access to the raw `.rvdata2` files.
3. **Translate**:
   - The text is displayed in a DataGrid.
   - Edit the "Translation" column.
   - Use `Ctrl+F` to find and replace text.
4. **Apply Translation**: Click "Apply" to generate the translated game files.
   - For MV/MZ: Creates a `TranslatedData` folder in the game directory.
   - For VX/Ace: Generates modified `.rvdata2` files.

## Keyboard Shortcuts

- **Ctrl + S**: Save the current project.
- **Ctrl + C**: Copy the text from the selected cell (or "Original Text" if the row is selected).
- **Shift + Enter**: Navigate to the next row while editing.
- **Enter**: Insert a new line (in editing mode).

## Built With

- [WPF-UI](https://github.com/lepoco/wpfui) - Fluent Design UI library.
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM architecture support.
- [Microsoft.EntityFrameworkCore.Sqlite](https://docs.microsoft.com/en-us/ef/core/) - Local database support (legacy/infrastructure).
- [Serilog](https://serilog.net/) - Logging.
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON handling.

## License

This project is licensed under the LGPL-3.0 License - see the [LICENSE](LICENSE) file for details.

---

# Nekomata (中文)

Nekomata 是一个专为 RPG Maker 游戏设计的翻译工具，支持 **RPG Maker MV, MZ, VX, 和 VX Ace**。它提供了一个现代化的界面来管理翻译项目和编辑游戏文本。

## 功能特性

- **项目管理**：基于文件的项目系统 (.nkproj)。
- **引擎支持**：
  - RPG Maker MV / MZ (.json)
  - RPG Maker VX / VX Ace (.rvdata2)
- **现代化 UI**：基于 WPF-UI 构建，拥有整洁的 Windows 11 风格界面。
- **翻译工作流**：
  - 导入游戏目录。
  - 在表格视图中编辑翻译。
  - 应用翻译以生成补丁后的游戏文件（非破坏性）。
- **文本处理**：
  - 合并连续的文本指令以提供更好的上下文。
  - 支持“复制原文”到剪贴板。

## 使用说明

1. **打开/创建项目**：启动 Nekomata 并创建一个新项目或打开现有的 `.nkproj` 文件。
2. **导入游戏**：点击“导入”并选择游戏目录。
   - **VX / VX Ace 特别说明**：导入前必须解包游戏归档（例如 `Game.rgss3a`）。Nekomata 需要读取原始的 `.rvdata2` 文件。
3. **翻译**：
   - 文本显示在数据表格中。
   - 编辑“Translation”列。
   - 使用 `Ctrl+F` 查找和替换文本。
4. **应用翻译**：点击“应用”生成翻译后的游戏文件。
   - MV/MZ：在游戏目录下创建 `TranslatedData` 文件夹。
   - VX/Ace：生成修改后的 `.rvdata2` 文件。

## 快捷键

- **Ctrl + S**：保存当前项目。
- **Ctrl + C**：复制选中单元格的文本（如果选中整行则复制原文）。
- **Shift + Enter**：编辑时跳转到下一行。
- **Enter**：插入换行符（在编辑模式下）。

## 使用的库

- [WPF-UI](https://github.com/lepoco/wpfui) - Fluent Design UI 库。
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM 架构支持。
- [Microsoft.EntityFrameworkCore.Sqlite](https://docs.microsoft.com/en-us/ef/core/) - 本地数据库支持（基础架构）。
- [Serilog](https://serilog.net/) - 日志记录。
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - JSON 处理。

## 许可证

本项目采用 LGPL-3.0 许可证 - 详情请参阅 [LICENSE](LICENSE) 文件。
