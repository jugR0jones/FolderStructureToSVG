# FolderStructureToSVG

A .NET console application that generates an interactive SVG visualisation of a folder structure.

## Overview

FolderStructureToSVG takes a path to a directory and recursively walks its contents, producing an SVG file that displays the folder hierarchy as a tree diagram. The generated SVG includes:

- **Folder and file icons** with distinct colours (orange for folders, blue for files)
- **Tree connector lines** showing parent–child relationships
- **Interactive folders** — click a folder to collapse or expand its contents (requires a web browser to view)
- **Automatic resizing** — the SVG height adjusts dynamically when folders are toggled

## Requirements

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later

## Building

```shell
dotnet build
```

## Usage

```
FolderStructureToSVG <folder-path> [output-file] [options]
```

### Arguments

| Argument | Required | Description |
|---|---|---|
| `<folder-path>` | Yes | Path to the folder to visualise. |
| `[output-file]` | No | Output SVG file path. Defaults to `structure.svg`. |

### Options

| Option | Short | Description |
|---|---|---|
| `--folders-only` | `-nf` | Only show folders in the output, ignoring all files. |
| `--exclude` | `-e` | Comma-separated list of file names or extensions to exclude from the output. Matching is case-insensitive. |

### Examples

Generate an SVG of a project folder:

```shell
FolderStructureToSVG "C:\Projects\MyApp"
```

Specify a custom output file:

```shell
FolderStructureToSVG "C:\Projects\MyApp" myapp-structure.svg
```

Show only the folder hierarchy (no files):

```shell
FolderStructureToSVG "C:\Projects\MyApp" --folders-only
```

Exclude specific file extensions:

```shell
FolderStructureToSVG "C:\Projects\MyApp" -e .dll,.pdb,.cache
```

Exclude specific file names:

```shell
FolderStructureToSVG "C:\Projects\MyApp" -e thumbs.db,.DS_Store
```

Mix file names and extensions:

```shell
FolderStructureToSVG "C:\Projects\MyApp" --exclude .json,.xml,README.md
```

> **Note:** If `--folders-only` and `--exclude` are used together, a warning is displayed and the exclude list is ignored, since files are already excluded.

## Viewing the Output

The generated SVG file contains embedded JavaScript for the interactive collapse/expand functionality. To use this feature, open the SVG in a **web browser** (Chrome, Firefox, Edge, Safari). Standard image viewers will render the diagram correctly but without interactivity.

