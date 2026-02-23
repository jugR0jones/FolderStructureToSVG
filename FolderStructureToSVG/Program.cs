using System.Text;

// Parse arguments
bool foldersOnly = false;
var positionalArgs = new List<string>();

foreach (var arg in args)
{
    if (arg is "--folders-only" or "-nf")
        foldersOnly = true;
    else
        positionalArgs.Add(arg);
}

if (positionalArgs.Count == 0)
{
    Console.WriteLine("Usage: FolderStructureToSVG <folder-path> [output-file] [--folders-only | -nf]");
    Console.WriteLine("  <folder-path>       Path to the folder to visualize.");
    Console.WriteLine("  [output-file]       Optional output SVG file path (default: structure.svg).");
    Console.WriteLine("  --folders-only -nf  Only show folders, ignore files.");
    return 1;
}

string folderPath = Path.GetFullPath(positionalArgs[0]);

if (!Directory.Exists(folderPath))
{
    Console.Error.WriteLine($"Error: The path \"{folderPath}\" does not exist or is not a directory.");
    return 1;
}

string outputFile = positionalArgs.Count >= 2 ? positionalArgs[1] : "structure.svg";

var root = BuildTree(folderPath, foldersOnly);
string svg = RenderSvg(root);

File.WriteAllText(outputFile, svg, new UTF8Encoding(false));
Console.WriteLine($"SVG written to: {Path.GetFullPath(outputFile)}");
return 0;

// ── Tree model ──────────────────────────────────────────────────────────────

TreeNode BuildTree(string path, bool dirsOnly)
{
    var dirInfo = new DirectoryInfo(path);
    var node = new TreeNode(dirInfo.Name, IsDirectory: true);

    try
    {
        foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            node.Children.Add(BuildTree(dir.FullName, dirsOnly));

        if (!dirsOnly)
        {
            foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                node.Children.Add(new TreeNode(file.Name, IsDirectory: false));
        }
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
    const int indentWidth = 20;
    const int midY = lineHeight / 2;

    int totalLines = CountNodes(rootNode);
    int svgHeight = paddingTop + totalLines * lineHeight + paddingBottom;

    int maxDepth = 0;
    int maxNameLen = 0;
    CollectMetrics(rootNode, 0, ref maxDepth, ref maxNameLen);
    int svgWidth = paddingLeft + (maxDepth + 1) * indentWidth + iconWidth + maxNameLen * 9 + 40;

    var sb = new StringBuilder();
    sb.Append($"""<?xml version="1.0" encoding="UTF-8"?><svg xmlns="http://www.w3.org/2000/svg" id="s" width="{svgWidth}" height="{svgHeight}" viewBox="0 0 {svgWidth} {svgHeight}">""");

    // Styles with short class names
    sb.Append("<style>"
        + ".fo{fill:#E8A87C}"
        + ".fi{fill:#95AABE}"
        + ".l{font-family:'Consolas','Courier New',monospace;font-size:14px;fill:#333}"
        + ".c{stroke:#999;stroke-width:1.5;fill:none}"
        + ".fr{cursor:pointer}"
        + ".fr:hover .l{fill:#0366d6}"
        + ".t{font-family:'Consolas',monospace;font-size:10px;fill:#999}"
        + "</style>");

    sb.Append($"""<rect id="b" width="{svgWidth}" height="{svgHeight}" rx="8" fill="#FAFAFA" stroke="#E0E0E0" stroke-width="1"/>""");

    // Reusable icon definitions
    sb.Append("<defs>"
        + """<g id="di"><rect y="4" width="14" height="4" rx="1" class="fo"/><rect y="6" width="16" height="10" rx="1" class="fo"/></g>"""
        + """<g id="fi"><rect x="1" y="3" width="12" height="14" rx="1" class="fi"/><polyline points="10,3 14,7" fill="none" stroke="#fff" stroke-width="1"/></g>"""
        + "</defs>");

    sb.Append($"""<g id="tree" transform="translate(0,{paddingTop})">""");
    nodeIdCounter = 0;
    currentY = 0;
    RenderNode(sb, rootNode, new List<bool>(), paddingLeft, lineHeight, iconWidth, indentWidth, midY);
    sb.Append("</g>");

    // Minified JavaScript
    sb.Append("<script type=\"text/ecmascript\"><![CDATA[");
    sb.Append($"var L={lineHeight},T={paddingTop},B={paddingBottom};");
    sb.Append(
        "function tg(e){"
        + "var r=e.currentTarget,i=r.getAttribute('data-node-id'),"
        + "c=document.getElementById('ch-'+i),"
        + "t=document.getElementById('t-'+i);"
        + "if(!c)return;"
        + "var h=c.getAttribute('data-c')==='1';"
        + "if(h){c.style.display='';c.setAttribute('data-c','0');if(t)t.textContent='\\u25BC'}"
        + "else{c.style.display='none';c.setAttribute('data-c','1');if(t)t.textContent='\\u25B6'}"
        + "lc()}"
        + "function lc(){"
        + "var t=document.getElementById('tree'),y=lg(t,0),"
        + "h=T+y+B,s=document.getElementById('s');"
        + "s.setAttribute('height',h);"
        + "s.setAttribute('viewBox','0 0 '+s.getAttribute('width')+' '+h);"
        + "document.getElementById('b').setAttribute('height',h)}"
        + "function lg(g,y){"
        + "for(var i=0;i<g.children.length;i++){"
        + "var c=g.children[i];"
        + "if(c.tagName==='g'&&c.classList.contains('n')){"
        + "var r=c.getElementsByClassName('r');"
        + "if(r.length>0){r[0].setAttribute('transform','translate(0,'+y+')');y+=L}"
        + "var ch=c.getElementsByClassName('ch');"
        + "if(ch.length>0&&ch[0].parentNode===c&&ch[0].style.display!=='none')y=lg(ch[0],y)}}"
        + "return y}"
        + "var f=document.querySelectorAll('.fr');"
        + "for(var i=0;i<f.length;i++)f[i].addEventListener('click',tg);"
        + "lc()");
    sb.Append("]]></script>");

    sb.Append("</svg>");
    return sb.ToString();
}

void RenderNode(StringBuilder sb, TreeNode node, List<bool> ancestorHasMore,
    float paddingLeft, int lineHeight, int iconWidth, int indentWidth, int midY)
{
    int currentId = nodeIdCounter++;
    int depth = ancestorHasMore.Count;
    bool hasChildren = node.Children.Count > 0;
    bool isClickable = node.IsDirectory && hasChildren;

    sb.Append($"<g class=\"n\" id=\"n-{currentId}\">");

    if (isClickable)
        sb.Append($"<g class=\"r fr\" data-node-id=\"{currentId}\" transform=\"translate(0,{currentY})\">");
    else
        sb.Append($"<g class=\"r\" transform=\"translate(0,{currentY})\">");

    currentY += lineHeight;

    // Connector lines
    if (depth > 0)
    {
        for (int i = 0; i < depth - 1; i++)
        {
            if (ancestorHasMore[i])
            {
                float lx = paddingLeft + i * indentWidth + indentWidth / 2f;
                sb.Append($"<line x1=\"{lx}\" y1=\"0\" x2=\"{lx}\" y2=\"{lineHeight}\" class=\"c\"/>");
            }
        }

        bool isLast = !ancestorHasMore[^1];
        float connX = paddingLeft + (depth - 1) * indentWidth + indentWidth / 2f;
        float connEndX = paddingLeft + depth * indentWidth;

        // Merged vertical: full height if not last, top-to-mid if last
        int vEnd = isLast ? midY : lineHeight;
        sb.Append($"<line x1=\"{connX}\" y1=\"0\" x2=\"{connX}\" y2=\"{vEnd}\" class=\"c\"/>");

        // Horizontal
        sb.Append($"<line x1=\"{connX}\" y1=\"{midY}\" x2=\"{connEndX}\" y2=\"{midY}\" class=\"c\"/>");
    }

    float x = paddingLeft + depth * indentWidth;

    // Toggle indicator
    if (isClickable)
        sb.Append($"<text id=\"t-{currentId}\" x=\"{x - 2}\" y=\"16\" class=\"t\">\u25BC</text>");

    // Icon via <use>
    if (node.IsDirectory)
        sb.Append($"<use href=\"#di\" x=\"{x}\"/>");
    else
        sb.Append($"<use href=\"#fi\" x=\"{x}\"/>");

    x += iconWidth;

    // Label
    if (node.IsDirectory)
        sb.Append($"<text x=\"{x}\" y=\"16\" class=\"l\" font-weight=\"bold\">{EscapeXml(node.Name)}</text>");
    else
        sb.Append($"<text x=\"{x}\" y=\"16\" class=\"l\">{EscapeXml(node.Name)}</text>");

    sb.Append("</g>");

    // Children
    if (hasChildren)
    {
        sb.Append($"<g class=\"ch\" id=\"ch-{currentId}\">");
        for (int i = 0; i < node.Children.Count; i++)
        {
            bool isLast = i == node.Children.Count - 1;
            ancestorHasMore.Add(!isLast);
            RenderNode(sb, node.Children[i], ancestorHasMore, paddingLeft, lineHeight, iconWidth, indentWidth, midY);
            ancestorHasMore.RemoveAt(ancestorHasMore.Count - 1);
        }
        sb.Append("</g>");
    }

    sb.Append("</g>");
}

int CountNodes(TreeNode node)
{
    int count = 1;
    foreach (var child in node.Children)
        count += CountNodes(child);
    return count;
}

void CollectMetrics(TreeNode node, int depth, ref int maxDepth, ref int maxNameLen)
{
    if (depth > maxDepth) maxDepth = depth;
    if (node.Name.Length > maxNameLen) maxNameLen = node.Name.Length;
    foreach (var child in node.Children)
        CollectMetrics(child, depth + 1, ref maxDepth, ref maxNameLen);
}

string EscapeXml(string text) =>
    text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

record TreeNode(string Name, bool IsDirectory)
{
    public List<TreeNode> Children { get; } = new();
}