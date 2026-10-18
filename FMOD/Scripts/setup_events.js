// Tạo (hoặc tạo lại) toàn bộ event âm thanh của game trong project FMOD Studio, rồi lưu và build bank.
// Chạy bằng: fmodstudiocl -script setup_events.js <project>.fspro
// Chạy lại nhiều lần được: event/bus/bank trùng tên sẽ được xoá rồi tạo lại, file âm thanh đã import thì dùng lại.

var SOURCE_DIR = "D:/Unity3D/survival-rts-01/FMOD/SourceAudio/";
var SAVE = true;

var ws = studio.project.workspace;

// ---------- Định nghĩa ----------
// is3D: dùng Spatialiser; minDist/maxDist (m) tính từ listener (đặt ở điểm camera nhìn xuống mặt đất).
// maxInstances: số tiếng cùng lúc tối đa của event; loop: phát lặp đến khi dừng.
var EVENTS = [
    { path: "Enemy/Hit", files: ["Enemy_Hit_01", "Enemy_Hit_02", "Enemy_Hit_03", "Enemy_Hit_04"], length: 0.25, is3D: true, maxInstances: 8, pitchRandom: 2.0, bus: "Combat" },
    { path: "Enemy/Death", files: ["Enemy_Death_01", "Enemy_Death_02", "Enemy_Death_03"], length: 0.8, is3D: true, maxInstances: 6, pitchRandom: 2.0, bus: "Combat" },
    { path: "Tower/Shot", files: ["Tower_Shot_01", "Tower_Shot_02", "Tower_Shot_03"], length: 0.4, is3D: true, maxInstances: 8, pitchRandom: 1.5, bus: "Combat" },
    { path: "Building/Collapse", files: ["Building_Collapse_01"], length: 1.9, is3D: true, maxInstances: 3, pitchRandom: 1.0, bus: "Combat" },
    { path: "Building/Construct", files: ["Builder_Hammer_01", "Builder_Hammer_02", "Builder_Hammer_03"], length: 0.35, is3D: true, maxInstances: 6, pitchRandom: 2.0, bus: "Combat" },
    { path: "Ambience/CrowdMarch", files: ["Crowd_March_Loop"], length: 2.0, is3D: false, maxInstances: 1, loop: true, bus: "Ambience" },
    { path: "Ambience/Battle", files: ["Battle_Loop"], length: 3.0, is3D: false, maxInstances: 1, loop: true, bus: "Ambience" },
    { path: "UI/Click", files: ["UI_Click"], length: 0.1, is3D: false, maxInstances: 2, bus: "UI" },
    { path: "UI/Place", files: ["UI_Place"], length: 0.35, is3D: false, maxInstances: 2, bus: "UI" },
    { path: "UI/AlertHQ", files: ["Alert_HQ"], length: 1.0, is3D: false, maxInstances: 1, bus: "UI" },
    { path: "UI/Open", files: ["UI_Open"], length: 0.4, is3D: false, maxInstances: 2, bus: "UI" },
    { path: "UI/Close", files: ["UI_Close"], length: 0.4, is3D: false, maxInstances: 2, bus: "UI" },
    { path: "Music/Calm", files: ["Music_Calm_Loop"], length: 16.0, is3D: false, maxInstances: 1, loop: true, bus: "Music" },
    { path: "Music/Battle", files: ["Music_Battle_Loop"], length: 8.0, is3D: false, maxInstances: 1, loop: true, bus: "Music" }
];
var MIN_DISTANCE = 3;
var MAX_DISTANCE = 45;
var BANK_NAME = "SFX";

// ---------- Tiện ích ----------
function findByName(type, name, filter) {
    var all = studio.project.model[type].findInstances();
    for (var i = 0; i < all.length; i++) {
        if (all[i].name === name && (!filter || filter(all[i]))) return all[i];
    }
    return null;
}

function getOrCreateFolder(parent, name) {
    var items = parent.items;
    for (var i = 0; i < items.length; i++) {
        if (items[i].isOfType("EventFolder") && items[i].name === name) return items[i];
    }
    var folder = studio.project.create("EventFolder");
    folder.name = name;
    folder.folder = parent;
    return folder;
}

function getOrCreateBus(name, output) {
    var bus = findByName("MixerGroup", name, function (b) { return b.isOfExactType("MixerGroup"); });
    if (!bus) {
        bus = studio.project.create("MixerGroup");
        bus.name = name;
    }
    bus.output = output;
    return bus;
}

function getOrCreateBank(name) {
    var bank = findByName("Bank", name);
    if (bank) return bank;
    bank = studio.project.create("Bank");
    bank.name = name;
    bank.folder = ws.masterBankFolder;
    return bank;
}

var importedCache = {};
function getAudioFile(folder, fileName) {
    var key = folder + "/" + fileName;
    if (importedCache[key]) return importedCache[key];
    var target = fileName + ".wav";
    var existing = studio.project.model.AudioFile.findInstances();
    for (var i = 0; i < existing.length; i++) {
        var p = existing[i].assetPath || "";
        if (p === target || p.slice(-target.length - 1) === "/" + target) {
            importedCache[key] = existing[i];
            return existing[i];
        }
    }
    var fullPath = SOURCE_DIR + folder + "/" + target;
    var audio = studio.project.importAudioFile(fullPath);
    if (!audio) throw new Error("Không import được " + fullPath);
    importedCache[key] = audio;
    return audio;
}

// ---------- Dựng ----------
// 3 nhóm âm lượng cho người chơi chỉnh: Music, SFX (gồm Combat và Ambience), UI. Unity chỉnh qua bus:/Music, bus:/SFX, bus:/UI.
var sfxBus = getOrCreateBus("SFX", ws.mixer.masterBus);
var buses = {
    Music: getOrCreateBus("Music", ws.mixer.masterBus),
    Combat: getOrCreateBus("Combat", sfxBus),
    Ambience: getOrCreateBus("Ambience", sfxBus),
    UI: getOrCreateBus("UI", ws.mixer.masterBus)
};
var bank = getOrCreateBank(BANK_NAME);

var created = [];
EVENTS.forEach(function (def) {
    var parts = def.path.split("/");
    var categoryFolder = parts[0];
    var folder = ws.masterEventFolder;
    for (var i = 0; i < parts.length - 1; i++) folder = getOrCreateFolder(folder, parts[i]);
    var eventName = parts[parts.length - 1];

    var old = folder.items.filter(function (it) { return it.isOfType("Event") && it.name === eventName; });
    old.forEach(function (it) { studio.project.deleteObject(it); });

    var ev = studio.project.create("Event");
    ev.name = eventName;
    ev.folder = folder;

    var track = ev.addGroupTrack("Audio");
    var sound = track.addSound(ev.timeline, "MultiSound", 0, def.length);
    def.files.forEach(function (fileName) {
        var single = studio.project.create("SingleSound");
        single.audioFile = getAudioFile(categoryFolder, fileName);
        // Cờ lặp của MultiSound chỉ lặp danh sách phát; phải đặt cả trên từng file thì event mới lặp vô hạn.
        if (def.loop) single.looping = true;
        sound.relationships.sounds.add(single);
    });
    if (def.loop) {
        sound.looping = true;
        sound.isAsync = true;
    }
    if (def.pitchRandom) {
        var mod = sound.addModulator("RandomizerModulator", "pitch");
        mod.amount = def.pitchRandom;
    }

    var group = ev.masterTrack.mixerGroup;
    group.maxInstances = def.maxInstances;
    if (def.is3D) {
        var spatialiser = group.effectChain.addEffect("SpatialiserEffect");
        spatialiser.overrideRange = true;
        spatialiser.minimumDistance = MIN_DISTANCE;
        spatialiser.maximumDistance = MAX_DISTANCE;
    }

    ev.mixerInput.output = buses[def.bus];
    ev.relationships.banks.add(bank);
    created.push(ev.getPath() + (def.is3D ? " [3D]" : " [2D]") + " max=" + def.maxInstances);
});

console.log("SETUP created " + created.length + " events:");
created.forEach(function (line) { console.log("SETUP   " + line); });

if (SAVE) {
    studio.project.save();
    console.log("SETUP saved");
    studio.project.build();
    console.log("SETUP built banks");
}
