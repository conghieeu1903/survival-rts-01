# Làm sạch JSON cài đặt (tab Âm thanh, Điều khiển) và dựng JSON "21 Offline - Menu Esc" theo SettingsPanel mới.
import copy, json, os

SCREENS = os.path.join(os.path.dirname(__file__), "..", "screens")

OFFLINE_CONTROLS = [
    ("Di chuyển camera", "W A S D"),
    ("Phóng to / thu nhỏ", "Cuộn chuột"),
    ("Đặt công trình", "Chuột trái"),
    ("Xoay công trình", "R"),
    ("Hủy đặt / hủy phá", "Chuột phải"),
    ("Chế độ tháo dỡ", "X"),
    ("Bản đồ lớn", "M"),
    ("Menu cài đặt", "Esc"),
]


def load(name):
    with open(os.path.join(SCREENS, name + ".json"), encoding="utf-8") as f:
        return json.load(f)


def save(name, data):
    with open(os.path.join(SCREENS, name + ".json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print("ghi", name)


def find(node, name):
    if node["name"] == name:
        return node
    for child in node.get("children", []):
        found = find(child, name)
        if found:
            return found
    return None


def group_title(group):
    return group["children"][0]["text"]["characters"]


# Bản xuất cũ còn sót nhóm của tab trước (Unity Destroy chậm một frame): chỉ giữ nhóm đúng tab.
def keep_group(data, prefix, height):
    content = find(data["nodes"][0], "Content")
    groups = [g for g in content["children"] if g["name"] == "Group" and group_title(g).startswith(prefix)]
    group = groups[-1]
    group["y"], group["h"] = 0, height
    content["children"] = [group]
    content["h"] = height
    return group


# Thanh tab cũng còn sót các nút của lần dựng trước: giữ 3 nút cuối và đặt lại vị trí.
def clean_tabs(data):
    tabs = find(data["nodes"][0], "Tabs")
    tabs["children"] = tabs["children"][-3:]
    for i, tab in enumerate(tabs["children"]):
        tab["x"] = i * 188


def set_footer(root, back_label, with_menu_button):
    footer = find(root, "Footer")
    kids = {k["name"]: k for k in footer["children"]}
    back, apply_btn, spacer = kids["Button - Quay lại"], kids["Button - Áp dụng"], kids["Spacer"]
    back["name"] = "Button - " + back_label
    back["children"][0]["text"]["characters"] = back_label
    back["children"][0]["name"] = "Text - " + back_label
    if with_menu_button:
        menu = copy.deepcopy(back)
        menu["name"] = "Button - Về menu chính"
        menu["w"] = 220
        menu["x"] = back["x"] - 12 - 220
        menu["children"][0]["name"] = "Text - Về menu chính"
        menu["children"][0]["text"]["characters"] = "Về menu chính"
        menu["children"][0]["w"] = 220
        footer["children"].insert(footer["children"].index(back), menu)
        spacer["w"] = menu["x"] - 12 - spacer["x"]


def esc_variant(source, screen_id, title):
    data = copy.deepcopy(source)
    data["screen"], data["title"] = screen_id, title
    root = data["nodes"][0]
    root["name"] = "SettingsMenu"
    root["visible"] = True
    root["fill"] = {"color": "#262626", "opacity": 0.45}
    set_footer(root, "Tiếp tục", True)
    return data


def build_offline_controls(source):
    data = copy.deepcopy(source)
    group = keep_group(data, ("Điều khiển", "Phím"), 0)
    template = next(c for c in group["children"] if c["name"] == "Setting")
    note = next(c for c in group["children"] if c["name"].startswith("Text - Chưa hỗ trợ"))
    group["children"][0]["text"]["characters"] = "Phím và thao tác"
    group["children"][0]["name"] = "Text - Phím và thao tác"
    rows = []
    for i, (name, key) in enumerate(OFFLINE_CONTROLS):
        row = copy.deepcopy(template)
        row["y"] = 42 + 58 * i
        title, line, key_text = row["children"]
        title["name"], title["text"]["characters"], title["x"], title["w"] = "Text - " + name, name, 4, 732
        key_text["name"], key_text["text"]["characters"], key_text["x"], key_text["w"] = "Text - " + key, key, 752, 756
        rows.append(row)
    note["y"] = 42 + 58 * len(OFFLINE_CONTROLS)
    group["children"] = [group["children"][0]] + rows + [note]
    height = note["y"] + note["h"]
    group["h"] = height
    content = find(data["nodes"][0], "Content")
    content["h"] = height
    return data


if __name__ == "__main__":
    graphics = load("08_cai_dat")
    audio = load("08b_cai_dat_am_thanh")
    controls = load("08c_cai_dat_dieu_khien")

    clean_tabs(audio)
    clean_tabs(controls)
    keep_group(audio, "Âm lượng", 272)
    controls_group = keep_group(controls, ("Điều khiển", "Phím"), 422)
    controls_group["children"][0]["text"]["characters"] = "Phím và thao tác"
    controls_group["children"][0]["name"] = "Text - Phím và thao tác"
    save("08b_cai_dat_am_thanh", audio)
    save("08c_cai_dat_dieu_khien", controls)

    save("21_offline_menu_esc", esc_variant(graphics, "21_offline_menu_esc", "Menu Esc offline - tab Đồ họa"))
    save("21b_offline_menu_esc_am_thanh", esc_variant(audio, "21b_offline_menu_esc_am_thanh", "Menu Esc offline - tab Âm thanh"))
    save("21c_offline_menu_esc_dieu_khien",
         esc_variant(build_offline_controls(controls), "21c_offline_menu_esc_dieu_khien", "Menu Esc offline - tab Điều khiển"))
