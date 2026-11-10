mergeInto(LibraryManager.library, {
    TurtleGame_ExitWebGame: function () {
        if (Module.turtleGameExiting) return;
        Module.turtleGameExiting = true;
        if (document.fullscreenElement && document.exitFullscreen) {
            try {
                var exit = document.exitFullscreen();
                if (exit && exit.catch) exit.catch(function () {});
            } catch (error) {}
        }
        var assets = Module.streamingAssetsUrl || "StreamingAssets";
        var base = new URL(assets.replace(/\/$/, "") + "/", document.baseURI);
        var exitPage = new URL("GameExit/index.html", base);
        exitPage.searchParams.set("game", window.location.href);
        // Unload only the game's frame, stopping Unity while itch.io stays open.
        // Yield so the current Unity callback finishes before its document unloads.
        window.setTimeout(function () {
            window.location.replace(exitPage.href);
        }, 150);
    }
});
