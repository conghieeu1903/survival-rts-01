// Plugin Figma: dựng lại giao diện Survival RTS từ các file JSON xuất bằng UiLayoutExporter (Unity).
// Mặc định đặt mọi layer theo toạ độ tuyệt đối để khớp từng pixel; tuỳ chọn Auto Layout.

figma.showUI(__html__, { width: 360, height: 360 });

const FONT_REGULAR = { family: 'Inter', style: 'Regular' };
const FONT_BOLD = { family: 'Inter', style: 'Bold' };

function rgb(hex) {
  const h = hex.replace('#', '');
  return { r: parseInt(h.slice(0, 2), 16) / 255, g: parseInt(h.slice(2, 4), 16) / 255, b: parseInt(h.slice(4, 6), 16) / 255 };
}

function solid(hex, opacity) {
  return { type: 'SOLID', color: rgb(hex), opacity: opacity === undefined ? 1 : opacity };
}

function size(value) {
  return Math.max(0.01, value);
}

const H_ALIGN = { LEFT: 'LEFT', CENTER: 'CENTER', RIGHT: 'RIGHT', JUSTIFIED: 'JUSTIFIED' };
const V_ALIGN = { TOP: 'TOP', CENTER: 'CENTER', BOTTOM: 'BOTTOM' };

// Tách chuỗi childAlignment của Unity (UpperLeft, MiddleCenter...) thành hàng và cột.
function splitAlign(name) {
  const row = name.startsWith('Upper') ? 'MIN' : name.startsWith('Lower') ? 'MAX' : 'CENTER';
  const col = name.endsWith('Left') ? 'MIN' : name.endsWith('Right') ? 'MAX' : 'CENTER';
  return { row, col };
}

function buildNode(data, parent, options, parentLayout) {
  let node;
  if (data.type === 'TEXT') {
    node = figma.createText();
    node.fontName = data.text.bold ? FONT_BOLD : FONT_REGULAR;
    node.characters = data.text.characters;
    node.fontSize = data.text.fontSize;
    node.fills = [solid(data.text.color, data.text.opacity)];
    node.textAlignHorizontal = H_ALIGN[data.text.alignH] || 'LEFT';
    node.textAlignVertical = V_ALIGN[data.text.alignV] || 'TOP';
    node.textAutoResize = 'NONE';
    node.resize(size(data.w), size(data.h));
  } else {
    node = figma.createFrame();
    node.fills = data.fill ? [solid(data.fill.color, data.fill.opacity)] : [];
    if (data.stroke) {
      node.strokes = [solid(data.stroke.color)];
      node.strokeWeight = data.stroke.width;
      node.strokeAlign = 'INSIDE';
    }
    if (data.cornerRadius) node.cornerRadius = data.cornerRadius;
    node.clipsContent = false;
    node.resize(size(data.w), size(data.h));
  }

  node.name = data.name;
  node.visible = data.visible !== false;
  parent.appendChild(node);
  node.x = data.x;
  node.y = data.y;

  const layout = data.autoLayout;
  if (data.children) {
    for (const child of data.children) buildNode(child, node, options, layout);
  }

  if (options.autoLayout && layout && data.type !== 'TEXT') applyAutoLayout(node, data, layout);
  if (options.autoLayout && parentLayout && data.layoutChild) applyChildLayout(node, data.layoutChild, parentLayout);
  return node;
}

function applyAutoLayout(node, data, layout) {
  const align = splitAlign(layout.align);
  const horizontal = layout.direction === 'HORIZONTAL';
  node.layoutMode = layout.direction;
  node.itemSpacing = layout.spacing;
  node.paddingLeft = layout.padding[0];
  node.paddingTop = layout.padding[1];
  node.paddingRight = layout.padding[2];
  node.paddingBottom = layout.padding[3];
  node.primaryAxisAlignItems = horizontal ? align.col : align.row;
  node.counterAxisAlignItems = horizontal ? align.row : align.col;
  node.primaryAxisSizingMode = 'FIXED';
  node.counterAxisSizingMode = 'FIXED';
  node.resize(size(data.w), size(data.h));
}

function applyChildLayout(node, child, parentLayout) {
  if (child.ignoreLayout) {
    node.layoutPositioning = 'ABSOLUTE';
    return;
  }
  const grow = parentLayout.direction === 'HORIZONTAL' ? child.growX : child.growY;
  if (grow > 0) node.layoutGrow = 1;
}

// Vẽ mũi tên thẳng từ mép frame nguồn tới mép frame đích, kèm nhãn ở giữa.
async function drawArrow(from, to, label) {
  const a = { x: from.x + from.width / 2, y: from.y + from.height / 2 };
  const b = { x: to.x + to.width / 2, y: to.y + to.height / 2 };
  const start = edgePoint(from, b);
  const end = edgePoint(to, a);

  const vector = figma.createVector();
  vector.name = 'Arrow - ' + label;
  await vector.setVectorNetworkAsync({
    vertices: [
      { x: start.x, y: start.y, strokeCap: 'NONE' },
      { x: end.x, y: end.y, strokeCap: 'ARROW_EQUILATERAL' }
    ],
    segments: [{ start: 0, end: 1 }]
  });
  vector.strokes = [solid('#505050')];
  vector.strokeWeight = 4;

  const text = figma.createText();
  text.fontName = FONT_BOLD;
  text.characters = label;
  text.fontSize = 28;
  text.fills = [solid('#262626')];
  text.x = (start.x + end.x) / 2 - text.width / 2;
  text.y = (start.y + end.y) / 2 - 40;
  text.name = 'Arrow label - ' + label;
}

// Điểm giao của tia từ tâm frame tới điểm khác với mép frame.
function edgePoint(frame, target) {
  const cx = frame.x + frame.width / 2;
  const cy = frame.y + frame.height / 2;
  const dx = target.x - cx;
  const dy = target.y - cy;
  if (dx === 0 && dy === 0) return { x: cx, y: cy };
  const scale = Math.min(
    dx !== 0 ? frame.width / 2 / Math.abs(dx) : Infinity,
    dy !== 0 ? frame.height / 2 / Math.abs(dy) : Infinity
  );
  return { x: cx + dx * scale, y: cy + dy * scale };
}

figma.ui.onmessage = async (msg) => {
  if (msg.type !== 'build') return;
  const say = (text) => figma.ui.postMessage({ type: 'log', text });

  await figma.loadFontAsync(FONT_REGULAR);
  await figma.loadFontAsync(FONT_BOLD);

  const flow = msg.files.find((f) => f.edges) || { screens: [], edges: [], grid: { colWidth: 2120, rowHeight: 1280 } };
  const screens = msg.files.filter((f) => f.nodes);
  const frames = {};
  let fallbackIndex = 0;

  for (const screen of screens) {
    const place = flow.screens.find((s) => s.id === screen.screen);
    const col = place ? place.col : fallbackIndex % 4;
    const row = place ? place.row : 6 + Math.floor(fallbackIndex / 4);
    if (!place) fallbackIndex++;

    const frame = figma.createFrame();
    frame.name = place ? place.title : screen.title;
    frame.resize(screen.frame.width, screen.frame.height);
    frame.x = col * flow.grid.colWidth;
    frame.y = row * flow.grid.rowHeight;
    frame.fills = [];
    frame.clipsContent = true;
    for (const node of screen.nodes) buildNode(node, frame, msg, null);
    figma.currentPage.appendChild(frame);
    frames[screen.screen] = frame;
    say('Vẽ: ' + frame.name);
  }

  if (msg.arrows) {
    for (const edge of flow.edges) {
      if (!frames[edge.from] || !frames[edge.to]) continue;
      await drawArrow(frames[edge.from], frames[edge.to], edge.label);
    }
    say('Đã vẽ mũi tên.');
  }

  figma.viewport.scrollAndZoomIntoView(Object.values(frames));
  figma.ui.postMessage({ type: 'done' });
};
