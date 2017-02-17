"use strict";
/// <reference path="types-gtanetwork/index.d.ts" />
var DrawnRay = (function () {
    function DrawnRay() {
    }
    return DrawnRay;
}());
var g_drawnRays = [];
var GameState;
(function (GameState) {
    GameState[GameState["Waiting"] = 0] = "Waiting";
    GameState[GameState["Hiding"] = 1] = "Hiding";
    GameState[GameState["Seeking"] = 2] = "Seeking";
    GameState[GameState["EndOfRound"] = 3] = "EndOfRound";
})(GameState || (GameState = {}));
var g_meta = {};
var g_playerList = [];
var g_gameState = GameState.Waiting;
var g_gameStateChanged = 0;
var g_myProp = null;
var g_rotationLocked = false;
var g_rotationLastSync = 0;
function getMyProp() {
    var player = API.getLocalPlayer();
    return API.getEntitySyncedData(player, "prophunt_prophandle");
}
function syncCurrentRotation() {
    var myProp = getMyProp();
    var rotation = API.getEntityRotation(myProp);
    API.triggerServerEvent("prophunt_rotation", rotation.Z);
}
function hasPlayer(handle) {
    for (var _i = 0, g_playerList_1 = g_playerList; _i < g_playerList_1.length; _i++) {
        var player = g_playerList_1[_i];
        if (player.Value == handle.Value) {
            return true;
        }
    }
    return false;
}
function initializePlayer(handle) {
    if (!hasPlayer) {
        g_playerList.push(handle);
    }
    API.setPlayerNametagVisible(handle, false);
    if (handle.Value == API.getLocalPlayer().Value) {
        g_myProp = getMyProp();
    }
}
function zeroPad(num, amount) {
    var ret = num.toString();
    if (ret.length >= amount) {
        return ret;
    }
    var zeroes = "";
    for (var i = 0; i < amount - ret.length; i++) {
        zeroes += "0";
    }
    return zeroes + ret;
}
API.onLocalPlayerShoot.connect(function (weaponUsed, aimCoords) {
    var newRay = new DrawnRay();
    newRay.time = API.getGameTime();
    newRay.from = API.getEntityPosition(API.getLocalPlayer());
    var dir = aimCoords.Subtract(newRay.from);
    var distance = dir.Length();
    dir.Normalize();
    // WORKAROUND: Can't seem to be able to use Vector3.Multiply in client scripts.
    // It's reported here: https://bt.gtanet.work/view.php?id=55
    dir.X *= distance * 1.05;
    dir.Y *= distance * 1.05;
    dir.Z *= distance * 1.05;
    newRay.to = newRay.from.Add(dir);
    var raycast = API.createRaycast(newRay.from, newRay.to, -1 /* Everything */, null);
    if (raycast.didHitEntity) {
        API.setEntityTransparency(raycast.hitEntity, 127);
        var player = API.getEntitySyncedData(raycast.hitEntity, "player");
        if (player != null && !player.IsNull) {
            API.sendChatMessage("Hit player: ~g~" + API.getPlayerName(player));
        }
        else {
            API.sendChatMessage("Hit entity but no player.");
        }
    }
    newRay.hit = raycast.didHitEntity;
    g_drawnRays.push(newRay);
});
API.onEntityStreamIn.connect(function (item, type) {
    // Is this still required? (Probably yes, but we need onEntityStreamOut to work as well)
    if (type == 6 || type == 8) {
        initializePlayer(item);
    }
});
API.onServerEventTrigger.connect(function (name, args) {
    API.sendNotification("Event: ~r~" + name);
    if (name == "prophunt_metadata") {
        g_meta = JSON.parse(args[0]);
    }
    if (name == "prophunt_begin") {
        g_gameState = GameState.Hiding;
        g_gameStateChanged = API.getGameTime();
    }
    if (name == "prophunt_state") {
        g_gameState = args[0];
        g_gameStateChanged = API.getGameTime();
    }
    if (name == "prophunt_end") {
        g_gameState = GameState.Waiting;
        g_myProp = null;
    }
    if (name == "prophunt_propset") {
        var client = args[0];
        if (client.IsNull) {
            return;
        }
        initializePlayer(client);
    }
    if (name == "prophunt_removeprop") {
        g_myProp = null;
        API.setEntityTransparency(API.getLocalPlayer(), 255);
    }
    if (name == "prophunt_rotation") {
        var propHandle = args[0];
        if (!propHandle.IsNull && (g_myProp == null || propHandle.Value != g_myProp.Value)) {
            var rotationZ = args[1];
            API.setEntityRotation(propHandle, new Vector3(0, 0, rotationZ));
        }
    }
});
API.onKeyDown.connect(function (sender, e) {
    if (e.KeyCode == Keys.E) {
        g_rotationLocked = !g_rotationLocked;
    }
});
API.onUpdate.connect(function () {
    for (var _i = 0, g_playerList_2 = g_playerList; _i < g_playerList_2.length; _i++) {
        var player = g_playerList_2[_i];
        var playerPos = API.getEntityPosition(player);
        var propHandle = API.getEntitySyncedData(player, "prophunt_prophandle");
        if (propHandle != null && !propHandle.IsNull) {
            API.setEntityPosition(propHandle, playerPos.Add(new Vector3(0, 0, -0.975)));
        }
    }
    var myHandle = API.getLocalPlayer();
    var isHiding = (g_myProp != null && !g_myProp.IsNull);
    if (isHiding) {
        if (!g_rotationLocked && g_myProp != null && !g_myProp.IsNull) {
            var rotation = API.getEntityRotation(g_myProp);
            rotation.Z = API.getEntityRotation(myHandle).Z;
            API.setEntityRotation(g_myProp, rotation);
            if (API.getGameTime() - g_rotationLastSync > 100) {
                syncCurrentRotation();
                g_rotationLastSync = API.getGameTime();
            }
        }
    }
    var screenResolution = API.getScreenResolutionMantainRatio();
    for (var i = 0; i < g_drawnRays.length; i++) {
        var ray = g_drawnRays[i];
        if (API.getGameTime() - ray.time > 200) {
            g_drawnRays.splice(i, 1);
            i--;
            continue;
        }
        if (ray.hit) {
            API.drawLine(ray.from, ray.to, 255, 255, 0, 0);
        }
        else {
            API.drawLine(ray.from, ray.to, 255, 0, 255, 0);
        }
    }
    if (g_gameState == GameState.Waiting) {
        API.drawText("Waiting for players...", screenResolution.Width / 2, screenResolution.Height * 0.3, 0.5, 255, 255, 255, 255, 0, 1, true, true, 0);
        return;
    }
    if (g_gameState == GameState.Hiding && !isHiding) {
        API.drawRectangle(0, 0, screenResolution.Width, screenResolution.Height, 30, 30, 30, 255);
    }
    var tmStateDuration = API.getGameTime() - g_gameStateChanged;
    if (g_gameState == GameState.Hiding) {
        var secondsLeft = Math.max(0, Math.ceil(((g_meta.tmHiding * 1000) - tmStateDuration) / 1000));
        var hidingText = "";
        if (isHiding) {
            hidingText = "Seekers will be released in: ~r~" + secondsLeft;
        }
        else {
            hidingText = "You will be unblinded in: ~r~" + secondsLeft;
        }
        API.drawText(hidingText, screenResolution.Width / 2, screenResolution.Height * 0.3, 0.5, 255, 255, 255, 255, 0, 1, true, true, 0);
    }
    if (g_gameState == GameState.Seeking) {
        var totalSecondsLeft = Math.max(0, Math.ceil(((g_meta.tmSeeking * 1000) - tmStateDuration) / 1000));
        var secondsLeft = totalSecondsLeft % 60;
        var minutesLeft = Math.floor(totalSecondsLeft / 60);
        API.drawText("Time left: ~r~" + minutesLeft + ":" + zeroPad(secondsLeft, 2), screenResolution.Width / 2, screenResolution.Height * 0.075, 0.5, 255, 255, 255, 255, 0, 1, true, true, 0);
    }
    if (g_gameState == GameState.EndOfRound) {
        var secondsLeft = Math.max(0, Math.ceil(((g_meta.tmEndOfRound * 1000) - tmStateDuration) / 1000));
        API.drawText("~b~Players~s~ win!", screenResolution.Width / 2, screenResolution.Height * 0.3, 0.5, 255, 255, 255, 255, 0, 1, true, true, 0);
        API.drawText("Restarting in: ~r~" + secondsLeft, screenResolution.Width / 2, screenResolution.Height * 0.3 + 30, 0.5, 255, 255, 255, 255, 0, 1, true, true, 0);
        return;
    }
    var teamText = "";
    if (isHiding) {
        teamText = "You are ~r~hiding";
    }
    else {
        teamText = "You are ~b~seeking";
    }
    API.drawText(teamText, screenResolution.Width / 2, screenResolution.Height * 0.15, 0.5, 255, 255, 255, 255, 0, 1, true, true, 0);
    if (isHiding) {
        var rotationStatus = "";
        if (g_rotationLocked) {
            rotationStatus = "~g~Locked";
        }
        else {
            rotationStatus = "~r~Unlocked";
        }
        API.drawText("Rotation: " + rotationStatus + "~s~. (E)", screenResolution.Width / 2, screenResolution.Height * 0.8, 0.5, 255, 255, 255, 255, 0, 1, true, true, 0);
    }
});
