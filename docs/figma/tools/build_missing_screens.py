# Dựng JSON cho các màn không xuất được từ Unity đang chạy: HUD trận, Kết quả, màn chơi offline cũ.
# Số liệu lấy từ BattleHudScreen.cs, BattleResultScreen.cs và UiPrefabGenerator.cs; layout tính lại theo luật của Unity LayoutGroup.
import json, os

OUT = os.path.join(os.path.dirname(__file__), "..", "screens")

# Màu theme sáng
INK, DIM, PANEL, ROW, BORDER, SEP, PRIMARY, PTEXT = "#262626", "#6C6C6C", "#FAFAFA", "#DADADA", "#808080", "#C0C0C0", "#505050", "#FAFAFA"
TEAM = ["#5A8CFF", "#FF6659", "#66E673", "#FFD94D"]
# Màu theme tối cũ
D_PANEL, D_CARD, D_TRACK, D_BORDER, D_TEXT, D_DIM, D_ACCENT, D_DARK = "#242424", "#383838", "#1A1A1A", "#7A7A7A", "#F5F5F5", "#C7C7C7", "#E6E6E6", "#141414"


class Node:
    def __init__(self, name, w=0.0, h=0.0, kind="FRAME", fill=None, opacity=1.0, stroke=None, stroke_w=2, radius=0,
                 text=None, button=False, visible=True):
        self.name, self.w, self.h, self.kind = name, w, h, kind
        self.x = self.y = 0.0
        self.fill, self.opacity, self.stroke, self.stroke_w, self.radius = fill, opacity, stroke, stroke_w, radius
        self.text, self.button, self.visible = text, button, visible
        self.children, self.layout = [], None
        self.grow = 0.0

    def add(self, *nodes):
        self.children.extend(nodes)
        return self

    # direction H/V, spacing, pad (trái, trên, phải, dưới), align (MIN/CENTER/MAX trục chính), expand: kéo giãn trục phụ
    def stack(self, direction, spacing=0.0, pad=(0, 0, 0, 0), align="MIN", cross="MIN", expand=True):
        if isinstance(pad, (int, float)):
            pad = (pad, pad, pad, pad)
        self.layout = dict(direction=direction, spacing=spacing, pad=pad, align=align, cross=cross, expand=expand)
        return self

    def hug(self):
        # Tự co theo con: kích thước ưa thích của các con cộng khoảng cách và lề
        lay = self.layout
        kids = [c for c in self.children if c.visible]
        l, t, r, b = lay["pad"]
        if lay["direction"] == "V":
            self.w = max([c.w for c in kids] + [0]) + l + r
            self.h = sum(c.h for c in kids) + lay["spacing"] * max(0, len(kids) - 1) + t + b
        else:
            self.w = sum(c.w for c in kids) + lay["spacing"] * max(0, len(kids) - 1) + l + r
            self.h = max([c.h for c in kids] + [0]) + t + b
        return self

    def do_layout(self):
        lay = self.layout
        for c in self.children:
            if c.layout:
                c.do_layout()
        if not lay:
            return
        l, t, r, b = lay["pad"]
        kids = [c for c in self.children if c.visible]
        inner_w, inner_h = self.w - l - r, self.h - t - b
        horizontal = lay["direction"] == "H"
        main_total = inner_w if horizontal else inner_h
        fixed = sum((c.w if horizontal else c.h) for c in kids if c.grow == 0)
        gaps = lay["spacing"] * max(0, len(kids) - 1)
        flex_sum = sum(c.grow for c in kids)
        free = main_total - fixed - gaps
        if flex_sum > 0:
            for c in kids:
                if c.grow > 0:
                    size = free * c.grow / flex_sum
                    if horizontal:
                        c.w = size
                    else:
                        c.h = size
            used = main_total
        else:
            used = fixed + gaps
        offset = {"MIN": 0, "CENTER": (main_total - used) / 2, "MAX": main_total - used}[lay["align"]]
        pos = (l if horizontal else t) + offset
        for c in kids:
            if lay["expand"]:
                if horizontal:
                    c.h = inner_h
                else:
                    c.w = inner_w
            if horizontal:
                c.x, c.y = pos, t
                pos += c.w + lay["spacing"]
            else:
                c.x, c.y = l, pos
                pos += c.h + lay["spacing"]

    def to_json(self):
        d = {"name": self.name, "type": self.kind, "x": r2(self.x), "y": r2(self.y), "w": r2(self.w), "h": r2(self.h),
             "visible": self.visible}
        if self.text:
            d["text"] = self.text
        else:
            if self.fill:
                d["fill"] = {"color": self.fill, "opacity": self.opacity}
            if self.stroke:
                d["stroke"] = {"color": self.stroke, "width": self.stroke_w}
            if self.radius:
                d["cornerRadius"] = self.radius
        if self.button:
            d["button"] = {"interactable": self.button != "disabled"}
        if self.layout:
            lay = self.layout
            align = lay["align"]
            h_name = {"MIN": "Left", "CENTER": "Center", "MAX": "Right"}
            v_name = {"MIN": "Upper", "CENTER": "Middle", "MAX": "Lower"}
            main, cross = align, "MIN" if lay["direction"] == "V" else "CENTER"
            if lay["direction"] == "H":
                unity_align = v_name[cross] + h_name[main]
            else:
                unity_align = v_name[main] + h_name["MIN"]
            d["autoLayout"] = {"direction": "HORIZONTAL" if lay["direction"] == "H" else "VERTICAL", "spacing": lay["spacing"],
                               "padding": list(lay["pad"]), "align": unity_align}
        if self.children and not self.text:
            d["children"] = [c.to_json() for c in self.children]
        return d


def r2(v):
    return round(v * 100) / 100


def text_node(s, size, color=INK, align="LEFT", valign="CENTER", bold=False, w=0, h=0, name=None, grow=0):
    n = Node(name or "Text - " + s.replace("\n", " ")[:28], w, h, kind="TEXT",
             text={"characters": s, "fontSize": size, "color": color, "opacity": 1, "bold": bold, "italic": False,
                   "alignH": align, "alignV": valign})
    n.grow = grow
    return n


# Thành phần theme sáng
def panel(name, w=0, h=0, fill=PANEL):
    return Node(name, w, h, fill=fill, stroke=BORDER, radius=6)


def button(label, w, h, size=20, primary=False, enabled=True):
    n = Node("Button - " + label.replace("\n", " ")[:28], w, h, fill=PRIMARY if primary else ROW, stroke=INK if primary else BORDER,
             radius=4, button=True if enabled else "disabled")
    t = text_node(label, size, PTEXT if primary else INK, "CENTER", "CENTER", w=w, h=h)
    n.add(t)
    return n


def swatch(color, w, h):
    return Node("Swatch", w, h, fill=color, stroke=BORDER, radius=4)


def sep(w):
    return Node("Separator", w, 2, fill=SEP)


def heading(text, size, w):
    box = Node("Heading", w, size * 1.4 + 8 + 2).stack("V", 8, 0)
    box.add(text_node(text, size, w=w, h=size * 1.4), sep(w))
    return box


def screen_json(screen_id, title, w, h, roots):
    return {"screen": screen_id, "title": title, "frame": {"width": w, "height": h}, "nodes": [r.to_json() for r in roots]}


def write(data):
    path = os.path.join(OUT, data["screen"] + ".json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print("ghi", path)


# ---------- 06 HUD trận ----------
def build_hud():
    W, H = 1920, 1080
    root = Node("Hud", W, H)

    top = panel("TopBar", W, 40).stack("H", 12, 8, expand=True)
    top.add(text_node("Lượt 120  |  Trễ 0 lượt", 18, grow=1), text_node("Đang chờ người chơi...", 18, align="CENTER", grow=1),
            text_node("Vàng: 400", 20, align="RIGHT", grow=1))
    top.do_layout()

    teams = panel("Teams", 240).stack("V", 4, 8)
    for i, (name, tag) in enumerate([("Đội 1", " (bạn)"), ("Đội 2", " (máy)"), ("Đội 3", " (máy)"), ("Đội 4", " (máy)")]):
        row = Node("Team" + str(i), 0, 28).stack("H", 8, 0)
        sw = swatch(TEAM[i], 20, 28)
        row.add(sw, text_node(f"{name}{tag}: 4000 lính", 18, grow=1))
        teams.add(row)
    teams.hug()
    teams.w = 240
    teams.do_layout()
    teams.x, teams.y = 8, 48

    train = panel("TrainBar").stack("H", 8, 8, expand=False)
    title = text_node("Huấn luyện", 18, DIM, "CENTER", "CENTER", w=110, h=60)
    train.add(title)
    for label in ("Lính kiếm\n200 vàng · 15s", "Lính cung\n200 vàng · 15s"):
        train.add(button(label, 190, 60, 16))
    train.hug()
    train.do_layout()
    train.x, train.y = (W - train.w) / 2, H - 8 - train.h
    for c in train.children:
        c.y = 8

    squads = panel("Squads", 260).stack("V", 6, 8)
    squads.add(text_node("Tiểu đội", 20, w=244, h=28), text_node("Đang chọn: 0 tiểu đội", 18, w=244, h=26),
               button("Chọn cả quân (Q)", 244, 36, 16), button("Lính kiếm: 28 đội · 2800 lính", 244, 36, 15),
               button("Lính cung: 12 đội · 1200 lính", 244, 36, 15),
               text_node("Kéo chuột: chọn khung\nShift: thêm vào\nNhấp đúp: cùng loại\nCtrl+1..9: gán nhóm\n1..9: gọi nhóm\nChuột phải: di chuyển",
                         14, DIM, "LEFT", "TOP", w=244, h=100))
    squads.hug()
    squads.w = 260
    squads.do_layout()
    squads.x, squads.y = W - 8 - squads.w, (H - squads.h) / 2

    notice = Node("Notice", 640, 56, fill=ROW, stroke=BORDER, radius=6).stack("H", 12, 8)
    notice.add(text_node("Phát hiện lệch đồng bộ ở lượt 120", 18, grow=1), button("Đóng", 90, 40, 16))
    notice.do_layout()
    notice.x, notice.y = (W - 640) / 2, 52
    notice.visible = False

    root.add(top, teams, train, squads, notice)
    return screen_json("06_hud_tran", "HUD trận đại chiến", W, H, [root])


# ---------- 07 Kết quả ----------
def build_result():
    W, H = 1920, 1080
    root = Node("Result", W, H, fill=INK, opacity=0.45)
    pnl = panel("Panel", 700).stack("V", 12, 24)
    inner = 652
    pnl.add(heading("Bạn thắng!", 32, inner))

    def row(name, cells, header, color=None):
        r = Node(name, inner, 40, fill=PANEL if header else ROW, stroke=BORDER, radius=6).stack("H", 10, 6)
        r.add(Node("SwatchSpace", 24, 0) if header else swatch(color, 24, 28))
        for i, c in enumerate(cells):
            r.add(text_node(c, 20, DIM if header else INK, "LEFT" if i == 0 else "CENTER", grow=1))
        return r

    pnl.add(row("Header", ["Đội", "Còn sống", "Đã hạ", "Đã mất"], True))
    data = [("Đội 1 (bạn)", "1250", "3400", "2750"), ("Đội 2", "0", "1100", "4000"), ("Đội 3", "0", "900", "4000"), ("Đội 4", "0", "700", "4000")]
    for i, d in enumerate(data):
        pnl.add(row("Team" + str(i), list(d), False, TEAM[i]))
    pnl.add(text_node("Chờ chủ phòng chọn...", 18, DIM, "CENTER", w=inner, h=28), sep(inner))
    actions = Node("Actions", inner, 52).stack("H", 12, 0, align="MAX")
    actions.add(button("Thoát", 130, 52), button("Về phòng chờ", 190, 52), button("Chơi lại", 150, 52, 20, True))
    pnl.add(actions)
    pnl.hug()
    pnl.w = 700
    pnl.do_layout()
    pnl.x, pnl.y = (W - 700) / 2, (H - pnl.h) / 2
    root.add(pnl)
    return screen_json("07_ket_qua", "Kết quả trận", W, H, [root])


# ---------- 20 màn chơi offline cũ (nền tối, 1440x810); menu Esc dựng bằng build_esc_menu.py ----------
def dpanel(name, w=0, h=0, fill=D_PANEL, opacity=0.95):
    return Node(name, w, h, fill=fill, opacity=opacity, stroke=D_BORDER, stroke_w=1)


def dbutton(label, w, h, bg=D_CARD, fg=D_TEXT):
    n = Node("Button - " + label[:28], w, h, fill=bg, stroke=D_BORDER, stroke_w=1, button=True)
    n.add(text_node(label, 12.5, fg, "CENTER", "CENTER", bold=True, w=w, h=h))
    return n


def badge(label, value, x):
    b = Node("Badge - " + label, 108, 42, fill=D_PANEL, opacity=0.95, stroke=D_BORDER, stroke_w=1)
    b.x = x
    icon = Node("icon", 26, 26, fill=D_CARD, stroke=D_BORDER, stroke_w=1)
    icon.x, icon.y = 8, 8
    info = Node("info", 54, 30)
    info.x, info.y = 42, 6
    info.add(text_node(label, 10.5, D_DIM, bold=True, w=54, h=14), text_node(value, 15, D_TEXT, bold=True, w=54, h=17))
    info.children[1].y = 15
    b.add(icon, info)
    return b


def build_offline_hud():
    W, H = 1440, 810
    root = Node("Offline", W, H)

    res = Node("resourceHud", 688, 42)
    res.x, res.y = W - 16 - 688, 14
    for i, (label, value) in enumerate([("DÂN SỐ", "2 / 5"), ("TIỀN", "0"), ("LÚA", "0"), ("GỖ", "0"), ("ĐÁ", "0"), ("SẮT", "0")]):
        res.add(badge(label, value, i * 116))

    mini = Node("minimapCanvas", 170, 170, fill="#808080", stroke=D_BORDER, stroke_w=1)
    mini.x, mini.y = 16, H - 16 - 170
    full = Node("fullMapCanvas", 460, 460, fill="#808080", stroke=D_BORDER, stroke_w=1, visible=False)
    full.x, full.y = (W - 460) / 2, (H - 460) / 2 - 36

    bp = dpanel("buildPanel").stack("V", 8, (12, 8, 12, 12), expand=False)
    header = Node("headerRow", 480, 20).stack("H", 18, 0)
    header.add(text_node("XÂY DỰNG", 13.5, D_TEXT, bold=True, w=80, h=20), text_node("Dân rỗi: 0/0  ·  Thợ xây: 0", 12.5, D_DIM, grow=1),
               text_node("Chuột trái đặt  ·  Chuột phải hủy", 12, D_DIM, "RIGHT", w=200, h=20))
    tabs = Node("tabRow", 480, 28).stack("H", 6, 0, expand=False)
    for label in ("Kinh tế", "Quân sự", "Phòng thủ"):
        tab = Node("Tab - " + label, 78, 28, fill=D_CARD, stroke=D_BORDER, stroke_w=1, button=True)
        tab.add(text_node(label, 12.5, D_TEXT, "LEFT", "CENTER", w=48, h=28))
        tab.children[0].x = 10
        cnt = Node("count", 16, 20, fill=D_TRACK)
        cnt.x, cnt.y = 58, 4
        cnt.add(text_node("0", 11.5, D_ACCENT, "CENTER", "CENTER", bold=True, w=16, h=20))
        tab.add(cnt)
        tabs.add(tab)
    lst = Node("buildList", 480, 120).stack("H", 8, 0, expand=False)
    for label, size, cost in (("Nhà ở", "2x2", "Miễn phí"), ("Trại thợ", "3x3", "50 gỗ")):
        card = Node("Card - " + label, 106, 120, fill=D_CARD, stroke=D_BORDER, stroke_w=1, button=True)
        prev = Node("cardPreview", 94, 46, fill=D_TRACK)
        prev.x, prev.y = 6, 5
        fp = Node("cardFootprint", 24, 24, fill="#949494", stroke=D_TEXT, stroke_w=1)
        fp.x, fp.y = 35, 11
        prev.add(fp)
        name = text_node(label, 12.5, D_TEXT, "CENTER", bold=True, w=96, h=19)
        name.x, name.y = 5, 53
        sz = text_node(size, 11, D_DIM, "CENTER", w=96, h=16)
        sz.x, sz.y = 5, 74
        cost_n = text_node(cost, 11.5, D_ACCENT, "CENTER", bold=True, w=96, h=18)
        cost_n.x, cost_n.y = 5, 92
        card.add(prev, name, sz, cost_n)
        lst.add(card)
    empty = text_node("Chưa có công trình nào trong nhóm này", 13, D_DIM, "CENTER", w=480, h=110)
    empty.visible = False
    placing = Node("placingBar", 480, 32).stack("H", 6, (0, 4, 0, 0))
    placing.add(text_node("Đang đặt", 12.5, D_TEXT, grow=1), dbutton("Xoay 90°", 86, 26), dbutton("Xác nhận phá", 112, 26), dbutton("Hủy", 68, 26))
    placing.visible = False
    bp.add(header, tabs, lst, empty, placing)
    bp.hug()
    bp.do_layout()
    # hàng tab: các tab đặt tay vì rộng theo nội dung
    for i, t in enumerate(tabs.children):
        t.x, t.y = i * 84, 0
    bp.x, bp.y = (W - bp.w) / 2, H - 16 - bp.h

    cmd = dpanel("commandPanel").stack("H", 10, (12, 8, 12, 8), expand=False)
    cmd.add(text_node("Lính kiếm", 14, D_TEXT, bold=True, w=70, h=30), text_node("Đang canh gác trước trại", 12.5, D_DIM, w=150, h=30),
            dbutton("Di chuyển", 88, 30), dbutton("Về trại", 78, 30))
    cmd.hug()
    cmd.do_layout()
    cmd.x, cmd.y = (W - cmd.w) / 2, H - 245 - cmd.h

    bar = Node("healthBar", 144, 52)
    bar.x, bar.y = 648, 320
    track = Node("barTrack", 144, 18, fill=D_TRACK, stroke=D_BORDER, stroke_w=1)
    track.y = 0
    fill = Node("fill", 144, 18, fill="#BFBFBF")
    track.add(fill, text_node("100 / 100", 11.5, D_TEXT, "CENTER", "CENTER", bold=True, w=144, h=18))
    bar.add(track)

    root.add(res, mini, full, bp, cmd, bar)
    return screen_json("20_offline_hud", "Màn chơi offline (cũ, nền tối)", W, H, [root])


if __name__ == "__main__":
    for builder in (build_hud, build_result, build_offline_hud):
        write(builder())
