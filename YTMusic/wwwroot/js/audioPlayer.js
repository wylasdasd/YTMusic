window.audioPlayer = {
    dotNetHelper: null,
    nativeAudio: null,
    nativeVideo: null,
    ogvPlayer: null,
    activePlayer: null,
    isSeeking: false,
    seekTimer: null,
    isVideoActive: false,
    pendingUrl: null,
    pendingIsWebM: false,
    pendingIsVideo: false,
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

    normalizeStreamUrl: function (url) {
        if (!url) return "";
        try {
            const parsed = new URL(url, window.location.origin);
            parsed.search = "";
            return parsed.toString();
        } catch {
            return url.split("?")[0];
        }
    },

    init: function (audioElement, videoElement, videoHost, helper) {
        this.dotNetHelper = helper;
        this.nativeAudio = audioElement;
        this.nativeVideo = videoElement || null;
        this.videoHost = videoHost || null;
        this.activePlayer = audioElement;

        if (typeof OGVPlayer !== 'undefined') {
            OGVLoader.base = 'https://cdnjs.cloudflare.com/ajax/libs/ogv.js/1.8.9';
            this.ogvPlayer = new OGVPlayer();
        }

        this.bindEvents(this.nativeAudio);
        this.bindEvents(this.nativeVideo);
        if (this.ogvPlayer) {
            this.bindEvents(this.ogvPlayer);
        }
    },

    bindEvents: function (player) {
        if (!player) return;
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

    updateVideoVisibility: function () {
        if (!this.nativeVideo) return;
        this.nativeVideo.style.display = this.isVideoActive ? "block" : "none";
    },

    attachVideoTo: function (container) {
        if (!this.nativeVideo || !container) return;
        if (this.nativeVideo.parentElement !== container) {
            container.appendChild(this.nativeVideo);
        }
        this.nativeVideo.classList.add("player-video");
        this.nativeVideo.style.display = "block";
        this.updateVideoVisibility();
    },

    detachVideo: function () {
        if (!this.nativeVideo || !this.videoHost) return;
        if (this.nativeVideo.parentElement !== this.videoHost) {
            this.videoHost.appendChild(this.nativeVideo);
        }
        this.nativeVideo.style.display = "none";
    },

    clearVideoElement: function () {
        if (!this.nativeVideo) return;
        if (this.activePlayer === this.nativeVideo) {
            this.activePlayer.pause();
            this.activePlayer.src = "";
            this.activePlayer.load();
            this.activePlayer = this.nativeAudio;
        }
        this.isVideoActive = false;
        this.detachVideo();
    },

    loadSource: function (url, isWebM, isVideo) {
        if (!url) return;

        const nextIsVideo = !!isVideo;
        const currentSrc = this.activePlayer && (this.activePlayer.currentSrc || this.activePlayer.src);

        this.pendingUrl = url;
        this.pendingIsWebM = !!isWebM;
        this.pendingIsVideo = nextIsVideo;
        this.isVideoActive = nextIsVideo;
        this.updateVideoVisibility();

        if (!this.nativeVideo && this.isVideoActive) {
            return;
        }

        // Use full URL: local file proxy shares one origin and only changes query/path hints.
        if (currentSrc && url === currentSrc && this.isVideoActive === nextIsVideo) {
            return;
        }

        this.isSeeking = false;

        if (this.activePlayer) {
            this.activePlayer.pause();
            this.activePlayer.src = "";
            this.activePlayer.load();
        }

        if (this.isVideoActive && this.nativeVideo) {
            this.activePlayer = this.nativeVideo;
        } else if (isWebM && this.ogvPlayer) {
            this.activePlayer = this.ogvPlayer;
        } else {
            this.activePlayer = this.nativeAudio;
        }

        this.activePlayer.src = url;
    },

    loadAndPlay: function (url, isWebM, isVideo) {
        this.loadSource(url, isWebM, isVideo);
        this.play();
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
