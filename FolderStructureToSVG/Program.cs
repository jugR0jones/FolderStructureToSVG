using System.Text;

if (args.Length == 0)
{
    Console.WriteLine("Usage: FolderStructureToSVG <folder-path> [output-file]");
    Console.WriteLine("  <folder-path>   Path to the folder to visualize.");
    Console.WriteLine("  [output-file]   Optional output SVG file path (default: structure.svg).");
    return 1;
}

string folderPath = Path.GetFullPath(args[0]);

if (!Directory.Exists(folderPath))
{
    Console.Error.WriteLine($"Error: The path \"{folderPath}\" does not exist or is not a directory.");
    return 1;
}

string outputFile = args.Length >= 2 ? args[1] : "structure.svg";

// Build the tree model
var root = BuildTree(folderPath);

// Render to SVG
string svg = RenderSvg(root);

File.WriteAllText(outputFile, svg, Encoding.UTF8);
Console.WriteLine($"SVG written to: {Path.GetFullPath(outputFile)}");
return 0;

// ── Tree model ──────────────────────────────────────────────────────────────

TreeNode BuildTree(string path)
{
    var dirInfo = new DirectoryInfo(path);
    var node = new TreeNode(dirInfo.Name, IsDirectory: true);

    try
    {
        foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            node.Children.Add(BuildTree(dir.FullName));
        }

        foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            node.Children.Add(new TreeNode(file.Name, IsDirectory: false));
        }
    }
    catch (UnauthorizedAccessException)
    {
        // Skip directories we can't access
    }

    return node;
}

// ── SVG rendering ───────────────────────────────────────────────────────────

string RenderSvg(TreeNode rootNode)
{
    const int lineHeight = 24;
    const int indentWidth = 20;
    const int paddingLeft = 16;
    const int paddingTop = 12;
    const int paddingBottom = 12;
    const int iconWidth = 18;
    const float charWidth = 8.5f;

    // Flatten the tree into lines with depth and connector prefixes
    var lines = new List<(int depth, string prefix, string name, bool isDir)>();
    FlattenTree(rootNode, lines, depth: 0, prefix: "");

    int totalLines = lines.Count;
    int svgHeight = paddingTop + totalLines * lineHeight + paddingBottom;

    // Calculate max width
    float maxWidth = 0;
    foreach (var (depth, prefix, name, _) in lines)
    {
        float width = paddingLeft + depth * indentWidth + prefix.Length * charWidth + iconWidth + name.Length * charWidth;
        if (width > maxWidth) maxWidth = width;
    }

    int svgWidth = (int)maxWidth + 40; // extra right padding

    var sb = new StringBuilder();
    sb.AppendLine($"""<?xml version="1.0" encoding="UTF-8"?>""");
    sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{svgWidth}" height="{svgHeight}" viewBox="0 0 {svgWidth} {svgHeight}">""");
    sb.AppendLine("""  <style>""");
    sb.AppendLine("""    .folder { fill: #E8A87C; }""");
    sb.AppendLine("""    .file   { fill: #95AABE; }""");
    sb.AppendLine("""    .label  { font-family: 'Consolas', 'Courier New', monospace; font-size: 14px; fill: #333; }""");
    sb.AppendLine("""    .connector { fill: #999; }""");
    sb.AppendLine("""  </style>""");
    sb.AppendLine($"""  <rect width="{svgWidth}" height="{svgHeight}" rx="8" fill="#FAFAFA" stroke="#E0E0E0" stroke-width="1"/>""");

    int y = paddingTop;
    foreach (var (depth, prefix, name, isDir) in lines)
    {
        float x = paddingLeft + depth * indentWidth;

        // Draw connector text (├── └── │ etc.)
        if (prefix.Length > 0)
        {
            sb.AppendLine($"""  <text x="{x}" y="{y + 16}" class="label connector">{EscapeXml(prefix)}</text>""");
            x += prefix.Length * charWidth;
        }

        // Draw icon
        if (isDir)
        {
            // Folder icon (simple rectangle)
            sb.AppendLine($"""  <rect x="{x}" y="{y + 4}" width="14" height="4" rx="1" class="folder"/>""");
            sb.AppendLine($"""  <rect x="{x}" y="{y + 6}" width="16" height="10" rx="1" class="folder"/>""");
        }
        else
        {
            // File icon
            sb.AppendLine($"""  <rect x="{x + 1}" y="{y + 3}" width="12" height="14" rx="1" class="file"/>""");
            sb.AppendLine($"""  <polyline points="{x + 9},{y + 3} {x + 13},{y + 7}" fill="none" stroke="#fff" stroke-width="1"/>""");
        }

        x += iconWidth;

        // Draw label
        string fontWeight = isDir ? "bold" : "normal";
        sb.AppendLine($"""  <text x="{x}" y="{y + 16}" class="label" font-weight="{fontWeight}">{EscapeXml(name)}</text>""");

        y += lineHeight;
    }

    sb.AppendLine("</svg>");
    return sb.ToString();
}

void FlattenTree(TreeNode node, List<(int depth, string prefix, string name, bool isDir)> lines, int depth, string prefix)
{
    lines.Add((depth, prefix, node.Name, node.IsDirectory));

    for (int i = 0; i < node.Children.Count; i++)
    {
        bool isLast = i == node.Children.Count - 1;
        string connector = isLast ? "└── " : "├── ";
        FlattenTree(node.Children[i], lines, depth + 1, connector);
    }
}

string EscapeXml(string text) =>
    text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

// ── Record types ────────────────────────────────────────────────────────────

record TreeNode(string Name, bool IsDirectory)
{
    public List<TreeNode> Children { get; } = new();
}
