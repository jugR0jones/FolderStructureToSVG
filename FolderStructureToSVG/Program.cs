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

var root = BuildTree(folderPath);
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
            node.Children.Add(BuildTree(dir.FullName));

        foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            node.Children.Add(new TreeNode(file.Name, IsDirectory: false));
    }
    catch (UnauthorizedAccessException) { }

    return node;
}

// ── SVG rendering ───────────────────────────────────────────────────────────

int nodeIdCounter = 0;
int currentY = 0;

string RenderSvg(TreeNode rootNode)
{
    const int lineHeight = 24;
    const int paddingLeft = 16;
    const int paddingTop = 12;
    const int paddingBottom = 12;
    const int iconWidth = 18;
    const float charWidth = 8.5f;

    int totalLines = CountNodes(rootNode);
    int svgHeight = paddingTop + totalLines * lineHeight + paddingBottom;

    var tempLines = new List<(string prefix, string name)>();
    CollectPrefixes(rootNode, tempLines, new List<bool>());
    float maxWidth = 0;
    foreach (var (prefix, name) in tempLines)
    {
        float width = paddingLeft + prefix.Length * charWidth + iconWidth + name.Length * charWidth;
        if (width > maxWidth) maxWidth = width;
    }
    int svgWidth = (int)maxWidth + 40;

    var sb = new StringBuilder();
    sb.AppendLine($"""<?xml version="1.0" encoding="UTF-8"?>""");
    sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" id="root-svg" width="{svgWidth}" height="{svgHeight}" viewBox="0 0 {svgWidth} {svgHeight}">""");
    sb.AppendLine("""  <style>""");
    sb.AppendLine("""    .folder { fill: #E8A87C; }""");
    sb.AppendLine("""    .file   { fill: #95AABE; }""");
    sb.AppendLine("""    .label  { font-family: 'Consolas', 'Courier New', monospace; font-size: 14px; fill: #333; }""");
    sb.AppendLine("""    .connector { fill: #999; }""");
    sb.AppendLine("""    .folder-row { cursor: pointer; }""");
    sb.AppendLine("""    .folder-row:hover .label { fill: #0366d6; }""");
    sb.AppendLine("""    .toggle { font-family: 'Consolas', monospace; font-size: 10px; fill: #999; }""");
    sb.AppendLine("""  </style>""");
    sb.AppendLine($"""  <rect id="bg" width="{svgWidth}" height="{svgHeight}" rx="8" fill="#FAFAFA" stroke="#E0E0E0" stroke-width="1"/>""");

    sb.AppendLine($"""  <g id="tree" transform="translate(0,{paddingTop})">""");
    nodeIdCounter = 0;
    currentY = 0;
    RenderNode(sb, rootNode, new List<bool>(), paddingLeft, lineHeight, iconWidth, charWidth);
    sb.AppendLine("  </g>");

    sb.AppendLine("""  <script type="text/ecmascript"><![CDATA[""");
    sb.AppendLine($$"""
    var lineHeight = {{lineHeight}};
    var paddingTop = {{paddingTop}};
    var paddingBottom = {{paddingBottom}};

    function toggleFolder(evt) {
      var row = evt.currentTarget;
      var nodeId = row.getAttribute('data-node-id');
      var childrenGroup = document.getElementById('children-' + nodeId);
      var toggleIcon = document.getElementById('toggle-' + nodeId);
      if (!childrenGroup) return;

      var isHidden = childrenGroup.getAttribute('data-collapsed') === 'true';
      if (isHidden) {
        childrenGroup.style.display = '';
        childrenGroup.setAttribute('data-collapsed', 'false');
        if (toggleIcon) toggleIcon.textContent = '\u25BC';
      } else {
        childrenGroup.style.display = 'none';
        childrenGroup.setAttribute('data-collapsed', 'true');
        if (toggleIcon) toggleIcon.textContent = '\u25B6';
      }

      recalculatePositions();
    }

    function recalculatePositions() {
      var tree = document.getElementById('tree');
      var currentY = 0;
      currentY = layoutGroup(tree, currentY);

      var totalHeight = paddingTop + currentY + paddingBottom;
      var svg = document.getElementById('root-svg');
      svg.setAttribute('height', totalHeight);
      svg.setAttribute('viewBox', '0 0 ' + svg.getAttribute('width') + ' ' + totalHeight);
      document.getElementById('bg').setAttribute('height', totalHeight);
    }

    function layoutGroup(group, y) {
      for (var i = 0; i < group.children.length; i++) {
        var child = group.children[i];
        if (child.tagName === 'g' && child.classList.contains('node-group')) {
          var rows = child.getElementsByClassName('row');
          if (rows.length > 0) {
            rows[0].setAttribute('transform', 'translate(0,' + y + ')');
            y += lineHeight;
          }
          var childGroups = child.getElementsByClassName('children-group');
          if (childGroups.length > 0 && childGroups[0].parentNode === child && childGroups[0].style.display !== 'none') {
            y = layoutGroup(childGroups[0], y);
          }
        }
      }
      return y;
    }

    // Attach click handlers
    var folders = document.querySelectorAll('.folder-row');
    for (var i = 0; i < folders.length; i++) {
      folders[i].addEventListener('click', toggleFolder);
    }

    // Initial layout on load
    recalculatePositions();
  """);
    sb.AppendLine("""  ]]></script>""");

    sb.AppendLine("</svg>");
    return sb.ToString();
}

void RenderNode(StringBuilder sb, TreeNode node, List<bool> ancestorHasMore,
    float paddingLeft, int lineHeight, int iconWidth, float charWidth)
{
    int currentId = nodeIdCounter++;

    var prefixBuilder = new StringBuilder();
    if (ancestorHasMore.Count > 0)
    {
        for (int i = 0; i < ancestorHasMore.Count - 1; i++)
            prefixBuilder.Append(ancestorHasMore[i] ? "│   " : "    ");
        prefixBuilder.Append(ancestorHasMore[^1] ? "├── " : "└── ");
    }
    string prefix = prefixBuilder.ToString();

    sb.AppendLine($"""    <g class="node-group" id="node-{currentId}">""");

    string rowClass = node.IsDirectory && node.Children.Count > 0 ? "row folder-row" : "row";
    string dataAttr = node.IsDirectory && node.Children.Count > 0 ? $""" data-node-id="{currentId}" """ : "";
    sb.AppendLine($"""      <g class="{rowClass}"{dataAttr} transform="translate(0,{currentY})">""");

    currentY += lineHeight;

    float x = paddingLeft;

    if (prefix.Length > 0)
    {
        sb.AppendLine($"""        <text x="{x}" y="16" class="label connector">{EscapeXml(prefix)}</text>""");
        x += prefix.Length * charWidth;
    }

    if (node.IsDirectory && node.Children.Count > 0)
        sb.AppendLine($"""        <text id="toggle-{currentId}" x="{x - 2}" y="16" class="toggle">▼</text>""");

    if (node.IsDirectory)
    {
        sb.AppendLine($"""        <rect x="{x}" y="4" width="14" height="4" rx="1" class="folder"/>""");
        sb.AppendLine($"""        <rect x="{x}" y="6" width="16" height="10" rx="1" class="folder"/>""");
    }
    else
    {
        sb.AppendLine($"""        <rect x="{x + 1}" y="3" width="12" height="14" rx="1" class="file"/>""");
        sb.AppendLine($"""        <polyline points="{x + 9},3 {x + 13},7" fill="none" stroke="#fff" stroke-width="1"/>""");
    }

    x += iconWidth;

    string fontWeight = node.IsDirectory ? "bold" : "normal";
    sb.AppendLine($"""        <text x="{x}" y="16" class="label" font-weight="{fontWeight}">{EscapeXml(node.Name)}</text>""");

    sb.AppendLine("      </g>");

    if (node.Children.Count > 0)
    {
        sb.AppendLine($"""      <g class="children-group" id="children-{currentId}" data-collapsed="false">""");
        for (int i = 0; i < node.Children.Count; i++)
        {
            bool isLast = i == node.Children.Count - 1;
            ancestorHasMore.Add(!isLast);
            RenderNode(sb, node.Children[i], ancestorHasMore, paddingLeft, lineHeight, iconWidth, charWidth);
            ancestorHasMore.RemoveAt(ancestorHasMore.Count - 1);
        }
        sb.AppendLine("      </g>");
    }

    sb.AppendLine("    </g>");
}

int CountNodes(TreeNode node)
{
    int count = 1;
    foreach (var child in node.Children)
        count += CountNodes(child);
    return count;
}

void CollectPrefixes(TreeNode node, List<(string prefix, string name)> list, List<bool> ancestorHasMore)
{
    var prefixBuilder = new StringBuilder();
    if (ancestorHasMore.Count > 0)
    {
        for (int i = 0; i < ancestorHasMore.Count - 1; i++)
            prefixBuilder.Append(ancestorHasMore[i] ? "│   " : "    ");
        prefixBuilder.Append(ancestorHasMore[^1] ? "├── " : "└── ");
    }
    list.Add((prefixBuilder.ToString(), node.Name));

    for (int i = 0; i < node.Children.Count; i++)
    {
        bool isLast = i == node.Children.Count - 1;
        ancestorHasMore.Add(!isLast);
        CollectPrefixes(node.Children[i], list, ancestorHasMore);
        ancestorHasMore.RemoveAt(ancestorHasMore.Count - 1);
    }
}

string EscapeXml(string text) =>
    text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

record TreeNode(string Name, bool IsDirectory)
{
    public List<TreeNode> Children { get; } = new();
}