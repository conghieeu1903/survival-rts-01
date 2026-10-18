// Tạo hoặc cập nhật toàn bộ event, parameter, bộ lọc EQ và sidechain trong project FMOD Studio, rồi lưu và build bank.
// Chạy bằng: fmodstudiocl -script setup_events.js <project>.fspro
// Giữ nguyên đối tượng Event có sẵn để không đổi GUID khi build lại.

var SOURCE_DIR = "D:/Unity3D/survival-rts-01/Assets/Audio/FMOD/SourceAudio/";
var SAVE = true;

var ws = studio.project.workspace;

// ---------- Định nghĩa ----------
// is3D: dùng Spatialiser; minDist/maxDist (m) tính từ listener đặt ở điểm camera nhìn xuống mặt đất.
// stealing = 1 (Quietest): khi vượt maxInstances, FMOD tự loại bỏ tiếng nhỏ/xa nhất để giữ tiếng gần camera nhất.
var EVENTS = [
    { path: "Enemy/Hit", files: ["Enemy_Hit_01", "Enemy_Hit_02", "Enemy_Hit_03", "Enemy_Hit_04"], length: 0.25, is3D: true, maxInstances: 8, pitchRandom: 2.0, bus: "Combat" },
    { path: "Enemy/Death", files: ["Enemy_Death_01", "Enemy_Death_02", "Enemy_Death_03"], length: 0.8, is3D: true, maxInstances: 6, pitchRandom: 2.0, bus: "Combat" },
    { path: "Tower/Shot", files: ["Tower_Shot_01", "Tower_Shot_02", "Tower_Shot_03"], length: 0.4, is3D: true, maxInstances: 8, pitchRandom: 1.5, bus: "Combat" },
    { path: "Building/Collapse", files: ["Building_Collapse_01"], length: 1.9, is3D: true, maxInstances: 3, pitchRandom: 1.0, bus: "Combat" },
    { path: "Building/Construct", files: ["Builder_Hammer_01", "Builder_Hammer_02", "Builder_Hammer_03"], length: 0.35, is3D: true, maxInstances: 6, pitchRandom: 1.5, bus: "Combat", buildProgressParam: true },
    { path: "Ambience/CrowdMarch", files: ["Crowd_March_Loop"], length: 2.0, is3D: false, maxInstances: 1, loop: true, bus: "Ambience" },
    { path: "Ambience/Battle", files: ["Battle_Loop"], length: 3.0, is3D: false, maxInstances: 1, loop: true, bus: "Ambience", intensityReactive: true },
    { path: "UI/Click", files: ["UI_Click"], length: 0.1, is3D: false, maxInstances: 2, bus: "UI" },
    { path: "UI/Place", files: ["UI_Place"], length: 0.35, is3D: false, maxInstances: 2, bus: "UI" },
    { path: "UI/AlertHQ", files: ["Alert_HQ"], length: 1.0, is3D: false, maxInstances: 1, bus: "UI" },
    { path: "UI/Open", files: ["UI_Open"], length: 0.4, is3D: false, maxInstances: 2, bus: "UI" },
    { path: "UI/Close", files: ["UI_Close"], length: 0.4, is3D: false, maxInstances: 2, bus: "UI" },
    { path: "Music/Calm", files: ["Music_Calm_Loop"], length: 16.0, is3D: false, maxInstances: 1, loop: true, bus: "Music" },
    { path: "Music/Battle", files: ["Music_Battle_Loop"], length: 8.0, is3D: false, maxInstances: 1, loop: true, bus: "Music", musicIntensityReactive: true }
];
var MIN_DISTANCE = 3;
var MAX_DISTANCE = 45;
var BANK_NAME = "SFX";
var STEALING_QUIETEST = 1;

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

function resetMixerEffectsAndModulators(group) {
    group.modulators.slice().forEach(function (m) { studio.project.deleteObject(m); });
    group.automators.slice().forEach(function (a) { studio.project.deleteObject(a); });
    group.effectChain.effects.slice().forEach(function (fx) {
        if (!fx.isOfExactType("MixerBusFader") && !fx.isOfExactType("MixerBusPanner")) {
            studio.project.deleteObject(fx);
        }
    });
}

function getOrCreateBus(name, output) {
    var bus = findByName("MixerGroup", name, function (b) { return b.isOfExactType("MixerGroup"); });
    if (!bus) {
        bus = studio.project.create("MixerGroup");
        bus.name = name;
    }
    bus.output = output;
    resetMixerEffectsAndModulators(bus);
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

function getOrCreateGlobalParam(name, min, max, initialValue, velocity) {
    var presets = studio.project.model.ParameterPreset.findInstances();
    for (var i = 0; i < presets.length; i++) {
        if (presets[i].name === name && presets[i].parameter) {
            var gp = presets[i].parameter;
            gp.isGlobal = true;
            gp.minimum = min;
            gp.maximum = max;
            gp.initialValue = initialValue;
            gp.velocity = velocity || 0;
            return gp;
        }
    }
    var created = ws.addGameParameter({ name: name, type: studio.project.parameterType.User, min: min, max: max });
    created.isGlobal = true;
    created.initialValue = initialValue;
    created.velocity = velocity || 0;
    return created;
}

function removeLocalParamPresetByName(name) {
    var presets = studio.project.model.ParameterPreset.findInstances();
    for (var i = 0; i < presets.length; i++) {
        if (presets[i].name === name && (!presets[i].parameter || !presets[i].parameter.isGlobal)) {
            studio.project.deleteObject(presets[i]);
        }
    }
}

function getOrResetEvent(folder, name) {
    var existing = folder.items.filter(function (it) { return it.isOfType("Event") && it.name === name; });
    if (existing.length > 1) {
        for (var i = 1; i < existing.length; i++) studio.project.deleteObject(existing[i]);
    }
    var ev = existing.length > 0 ? existing[0] : null;
    if (!ev) {
        ev = studio.project.create("Event");
        ev.name = name;
        ev.folder = folder;
        return ev;
    }
    ev.groupTracks.slice().forEach(function (t) { studio.project.deleteObject(t); });
    ev.parameters.slice().forEach(function (p) { studio.project.deleteObject(p); });
    resetMixerEffectsAndModulators(ev.masterTrack.mixerGroup);
    return ev;
}

function addTwoPointCurve(target, propertyName, parameter, x0, y0, x1, y1) {
    var automator = target.addAutomator(propertyName);
    var curve = automator.addAutomationCurve(parameter);
    curve.addAutomationPoint(x0, y0);
    curve.addAutomationPoint(x1, y1);
    return curve;
}

function addThreePointCurve(target, propertyName, parameter, x0, y0, x1, y1, x2, y2) {
    var automator = target.addAutomator(propertyName);
    var curve = automator.addAutomationCurve(parameter);
    curve.addAutomationPoint(x0, y0);
    curve.addAutomationPoint(x1, y1);
    curve.addAutomationPoint(x2, y2);
    return curve;
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

// ---------- Dựng Parameter toàn cục & Mixer Bus ----------
// CameraZoom (0 = zoom xa trên cao, 1 = zoom sát mặt đất) và CombatIntensity (0 = yên, 1 = giao tranh lớn).
var cameraZoomParam = getOrCreateGlobalParam("CameraZoom", 0, 1, 1, 2.5);
var combatIntensityParam = getOrCreateGlobalParam("CombatIntensity", 0, 1, 0, 1.2);

var sfxBus = getOrCreateBus("SFX", ws.mixer.masterBus);
var buses = {
    Music: getOrCreateBus("Music", ws.mixer.masterBus),
    Combat: getOrCreateBus("Combat", sfxBus),
    Ambience: getOrCreateBus("Ambience", sfxBus),
    UI: getOrCreateBus("UI", ws.mixer.masterBus)
};

// 1. Lọc tần số theo độ cao Camera: zoom xa thì hạ dải cao/trung nghe trầm vọng từ trên cao, zoom gần mở hết dải tần.
[buses.Combat, buses.Ambience].forEach(function (bus) {
    var eq = bus.effectChain.addEffect("ThreeEQEffect");
    eq.lowCrossover = 350;
    eq.highCrossover = 3200;
    addThreePointCurve(eq, "highGain", cameraZoomParam, 0, -22, 0.5, -5, 1, 0);
    addThreePointCurve(eq, "midGain", cameraZoomParam, 0, -6, 0.5, -1.5, 1, 0);
});

// 2. Sidechain Ducking tự động trên Mixer:
// Khi có tiếng giao tranh lớn (Combat) hoặc cảnh báo/sự kiện UI, tự động nén nhẹ Music và Ambience xuống vài dB.
var combatSidechain = buses.Combat.effectChain.addEffect("Sidechain");
var uiSidechain = buses.UI.effectChain.addEffect("Sidechain");

var musicDuck = buses.Music.addModulator("SidechainModulator", "volume");
musicDuck.amount = 4.5;
musicDuck.minimumThreshold = -28;
musicDuck.maximumThreshold = -6;
musicDuck.attackTime = 35;
musicDuck.releaseTime = 380;
combatSidechain.relationships.targets.add(musicDuck);
uiSidechain.relationships.targets.add(musicDuck);

var ambienceDuck = buses.Ambience.addModulator("SidechainModulator", "volume");
ambienceDuck.amount = 6.0;
ambienceDuck.minimumThreshold = -26;
ambienceDuck.maximumThreshold = -6;
ambienceDuck.attackTime = 20;
ambienceDuck.releaseTime = 260;
combatSidechain.relationships.targets.add(ambienceDuck);

// ---------- Dựng Events ----------
var bank = getOrCreateBank(BANK_NAME);
removeLocalParamPresetByName("BuildProgress");

var created = [];
EVENTS.forEach(function (def) {
    var parts = def.path.split("/");
    var categoryFolder = parts[0];
    var folder = ws.masterEventFolder;
    for (var i = 0; i < parts.length - 1; i++) folder = getOrCreateFolder(folder, parts[i]);
    var eventName = parts[parts.length - 1];

    var ev = getOrResetEvent(folder, eventName);

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
    group.instanceStealing = STEALING_QUIETEST;
    ev.automatableProperties.maxVoices = def.maxInstances;
    ev.automatableProperties.voiceStealing = STEALING_QUIETEST;

    // 3a. Parameter cục bộ BuildProgress (0..1) trên Building/Construct: càng gần xây xong tiếng búa càng đanh và sáng.
    if (def.buildProgressParam) {
        var proxy = ev.addGameParameter({ name: "BuildProgress", type: studio.project.parameterType.User, min: 0, max: 1 });
        var buildParam = proxy.preset;
        addTwoPointCurve(sound, "pitch", buildParam, 0, -2.0, 1, 3.5);
        var constructEq = group.effectChain.addEffect("ThreeEQEffect");
        addTwoPointCurve(constructEq, "highGain", buildParam, 0, -6.0, 1, 3.0);
    }

    // 3b. Ambience/Battle tự biến đổi cao độ và độ sáng theo mật độ giao tranh (CombatIntensity).
    if (def.intensityReactive) {
        addTwoPointCurve(sound, "pitch", combatIntensityParam, 0, -1.0, 1, 1.5);
        var battleEq = group.effectChain.addEffect("ThreeEQEffect");
        addTwoPointCurve(battleEq, "highGain", combatIntensityParam, 0, -6.0, 1, 2.0);
    }

    // 3c. Music/Battle dày tiếng trống trầm và sáng hơn khi giao tranh căng thẳng (CombatIntensity).
    if (def.musicIntensityReactive) {
        var musicEq = group.effectChain.addEffect("ThreeEQEffect");
        addTwoPointCurve(musicEq, "lowGain", combatIntensityParam, 0, 0.0, 1, 2.5);
        addTwoPointCurve(musicEq, "highGain", combatIntensityParam, 0, -3.5, 1, 1.5);
    }

    if (def.is3D) {
        var spatialiser = group.effectChain.addEffect("SpatialiserEffect");
        spatialiser.overrideRange = true;
        spatialiser.minimumDistance = MIN_DISTANCE;
        spatialiser.maximumDistance = MAX_DISTANCE;
    }

    ev.mixerInput.output = buses[def.bus];
    ev.relationships.banks.add(bank);
    created.push(ev.getPath() + (def.is3D ? " [3D]" : " [2D]") + " max=" + def.maxInstances + " steal=Quietest");
});

console.log("SETUP configured " + created.length + " events:");
created.forEach(function (line) { console.log("SETUP   " + line); });

if (SAVE) {
    studio.project.save();
    console.log("SETUP saved");
    studio.project.build();
    console.log("SETUP built banks");
}
