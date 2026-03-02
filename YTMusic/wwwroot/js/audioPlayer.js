window.audioPlayer = {
    dotNetHelper: null,
    nativeAudio: null,
    ogvPlayer: null,
    activePlayer: null,
    isSeeking: false,
    seekTimer: null,
    describeMediaError: function (player) {
        if (!player || !player.error) return "none";
        const code = player.error.code;
        const map = {
            1: "MEDIA_ERR_ABORTED",
            2: "MEDIA_ERR_NETWORK",
            3: "MEDIA_ERR_DECODE",
            4: "MEDIA_ERR_SRC_NOT_SUPPORTED"
        };
        return `${map[code] || "UNKNOWN"}(${code})`;
    },
    reportError: function (tag, player, err) {
        const payload = {
            tag: tag,
            errName: err && err.name ? err.name : "",
            errMessage: err && err.message ? err.message : String(err || ""),
            mediaError: this.describeMediaError(player),
            src: player && player.currentSrc ? player.currentSrc : (player && player.src ? player.src : ""),
            readyState: player && typeof player.readyState === "number" ? player.readyState : -1,
            networkState: player && typeof player.networkState === "number" ? player.networkState : -1,
            paused: !!(player && player.paused),
            ended: !!(player && player.ended),
            currentTime: player && typeof player.currentTime === "number" ? player.currentTime : 0
        };

        console.error("audioPlayer diagnostic:", payload);
        if (this.dotNetHelper) {
            this.dotNetHelper.invokeMethodAsync('OnPlaybackError', JSON.stringify(payload));
        }
    },

    init: function (element, helper) {
        this.dotNetHelper = helper;
        this.nativeAudio = element;
        this.activePlayer = element;

        if (typeof OGVPlayer !== 'undefined') {
            OGVLoader.base = 'https://cdnjs.cloudflare.com/ajax/libs/ogv.js/1.8.9';
            this.ogvPlayer = new OGVPlayer();
        }

        this.bindEvents(this.nativeAudio);
        if (this.ogvPlayer) {
            this.bindEvents(this.ogvPlayer);
        }
    },

    bindEvents: function (player) {
        player.ontimeupdate = function () {
            if (window.audioPlayer.isSeeking) return; // Guard
            if (window.audioPlayer.activePlayer === player && window.audioPlayer.dotNetHelper) {
                window.audioPlayer.dotNetHelper.invokeMethodAsync('OnTimeUpdate', player.currentTime);
            }
        };
        player.onseeked = function () {
            window.audioPlayer.isSeeking = false;
        };
        player.onloadedmetadata = function () {
            if (window.audioPlayer.activePlayer === player && window.audioPlayer.dotNetHelper) {
                window.audioPlayer.dotNetHelper.invokeMethodAsync('OnLoadedMetadata', player.duration);
            }
        };
        player.onended = function () {
            if (window.audioPlayer.activePlayer === player && window.audioPlayer.dotNetHelper) {
                window.audioPlayer.dotNetHelper.invokeMethodAsync('OnEnded');
            }
        };
        player.onplay = function () {
            if (window.audioPlayer.activePlayer === player && window.audioPlayer.dotNetHelper) {
                window.audioPlayer.dotNetHelper.invokeMethodAsync('OnPlayStateChanged', true);
            }
        };
        player.onpause = function () {
            if (window.audioPlayer.activePlayer === player && window.audioPlayer.dotNetHelper) {
                window.audioPlayer.dotNetHelper.invokeMethodAsync('OnPlayStateChanged', false);
            }
        };
        player.onerror = function () {
            window.audioPlayer.reportError("media.onerror", player, player.error || new Error("media element error"));
        };
        player.onstalled = function () {
            console.warn("audioPlayer stalled", { src: player.currentSrc || player.src || "" });
        };
        player.onwaiting = function () {
            console.warn("audioPlayer waiting", { src: player.currentSrc || player.src || "" });
        };
    },

    loadAndPlay: function (url, isWebM) {
        if (!url) return;

        this.isSeeking = false;
        if (this.activePlayer) {
            this.activePlayer.pause();
            this.activePlayer.src = "";
            this.activePlayer.load();
        }

        if (isWebM && this.ogvPlayer) {
            this.activePlayer = this.ogvPlayer;
        } else {
            this.activePlayer = this.nativeAudio;
        }

        this.activePlayer.src = url;
        this.activePlayer.play().catch(e => this.reportError("loadAndPlay.play", this.activePlayer, e));
    },

    play: function () {
        if (this.activePlayer) {
            this.activePlayer.play().catch(e => this.reportError("play", this.activePlayer, e));
        }
    },
    pause: function () {
        if (this.activePlayer) {
            this.activePlayer.pause();
        }
    },
    setCurrentTime: function (time) {
        if (this.activePlayer) {
            this.isSeeking = true;
            this.activePlayer.currentTime = time;

            // Backup safety: if seeked event doesn't fire (e.g. error), clear flag eventually
            if (this.seekTimer) clearTimeout(this.seekTimer);
            this.seekTimer = setTimeout(() => { this.isSeeking = false; }, 2000);
        }
    },
    dispose: function () {
        this.dotNetHelper = null;
    }
};
