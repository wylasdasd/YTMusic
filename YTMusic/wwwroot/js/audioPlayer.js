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
    useNativeProgressMode: false,
    lastDotNetTimeReport: 0,
    dotnetTimeIntervalMs: 500,
    progressRafId: null,
    progressRoot: null,
    fullscreenTarget: null,
    fullscreenHelper: null,
    fullscreenEventsBound: false,
    pendingWebVideoShouldPlay: false,
    pendingWebVideoTime: null,
    webVideoCanPlayHandler: null,
    progressBar: {
        container: null,
        slider: null,
        currentEl: null,
        durationEl: null,
        isDragging: false,
        seekThrottleTimer: null
    },

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
    log: function (message, extra) {
        const line = extra === undefined
            ? `[YTMusic:Playback] ${message}`
            : `[YTMusic:Playback] ${message} ${JSON.stringify(extra)}`;
        console.log(line);
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

    formatTime: function (seconds) {
        if (!Number.isFinite(seconds) || seconds < 0) {
            return "00:00";
        }
        const total = Math.floor(seconds);
        const mm = String(Math.floor(total / 60)).padStart(2, "0");
        const ss = String(total % 60).padStart(2, "0");
        return `${mm}:${ss}`;
    },

    init: function (audioElement, videoElement, videoHost, helper) {
        this.dotNetHelper = helper;
        this.nativeAudio = audioElement;
        this.nativeVideo = videoElement || null;
        this.videoHost = videoHost || null;
        if (this.nativeVideo) {
            this.nativeVideo.controls = false;
        }
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

    setNativeProgressMode: function (enabled) {
        this.useNativeProgressMode = !!enabled;
        if (enabled) {
            this.stopProgressLoop();
        } else if (this.activePlayer && !this.activePlayer.paused) {
            this.startProgressLoop();
        }
    },

    mountProgressBar: function (container) {
        if (!container) return;

        if (!this.progressRoot) {
            const root = document.createElement("div");
            root.className = "ytm-player-progress";

            const slider = document.createElement("input");
            slider.type = "range";
            slider.className = "ytm-player-progress__slider";
            slider.min = "0";
            slider.max = "100";
            slider.step = "0.1";
            slider.value = "0";
            slider.setAttribute("aria-label", "Playback progress");

            const times = document.createElement("div");
            times.className = "ytm-player-progress__times";

            const currentEl = document.createElement("span");
            currentEl.className = "ytm-player-progress__time";
            currentEl.textContent = "00:00";

            const durationEl = document.createElement("span");
            durationEl.className = "ytm-player-progress__time";
            durationEl.textContent = "00:00";

            times.appendChild(currentEl);
            times.appendChild(durationEl);
            root.appendChild(slider);
            root.appendChild(times);

            const self = window.audioPlayer;
            slider.addEventListener("pointerdown", function () {
                self.progressBar.isDragging = true;
            });
            slider.addEventListener("pointerup", function () {
                self.progressBar.isDragging = false;
            });
            slider.addEventListener("input", function () {
                self.progressBar.isDragging = true;
                const t = parseFloat(slider.value);
                if (Number.isFinite(t)) {
                    currentEl.textContent = self.formatTime(t);
                }
                if (self.progressBar.seekThrottleTimer) {
                    return;
                }
                self.progressBar.seekThrottleTimer = setTimeout(function () {
                    self.progressBar.seekThrottleTimer = null;
                    const seekTime = parseFloat(slider.value);
                    if (Number.isFinite(seekTime)) {
                        self.seekProgressTo(seekTime);
                    }
                }, 120);
            });
            slider.addEventListener("change", function () {
                const t = parseFloat(slider.value);
                if (self.progressBar.seekThrottleTimer) {
                    clearTimeout(self.progressBar.seekThrottleTimer);
                    self.progressBar.seekThrottleTimer = null;
                }
                if (Number.isFinite(t)) {
                    self.seekProgressTo(t);
                }
                self.progressBar.isDragging = false;
            });

            this.progressRoot = root;
            this.progressBar.slider = slider;
            this.progressBar.currentEl = currentEl;
            this.progressBar.durationEl = durationEl;
        }

        container.replaceChildren(this.progressRoot);
        this.progressBar.container = container;
    },

    unmountProgressBar: function () {
        if (this.progressRoot && this.progressRoot.parentElement) {
            this.progressRoot.parentElement.removeChild(this.progressRoot);
        }
        this.progressBar.container = null;
    },

    setProgress: function (current, duration, force) {
        if (this.progressBar.isDragging && !force) {
            return;
        }

        const slider = this.progressBar.slider;
        if (!slider) {
            return;
        }

        const d = Number.isFinite(duration) && duration > 0 ? duration : 100;
        const c = Number.isFinite(current) ? Math.max(0, Math.min(current, d)) : 0;

        slider.max = String(d);
        slider.value = String(c);

        if (this.progressBar.currentEl) {
            this.progressBar.currentEl.textContent = this.formatTime(c);
        }
        if (this.progressBar.durationEl) {
            this.progressBar.durationEl.textContent = this.formatTime(d);
        }
    },

    updateProgressFromPlayer: function () {
        if (this.progressBar.isDragging || this.useNativeProgressMode) {
            return;
        }

        const player = this.activePlayer;
        if (!player || typeof player.currentTime !== "number") {
            return;
        }

        const duration = Number.isFinite(player.duration) && player.duration > 0 ? player.duration : 100;
        this.setProgress(player.currentTime, duration, false);
    },

    startProgressLoop: function () {
        if (this.progressRafId || this.useNativeProgressMode) {
            return;
        }

        const self = this;
        const loop = function () {
            self.progressRafId = null;
            if (!self.useNativeProgressMode) {
                self.updateProgressFromPlayer();
            }
            if (self.activePlayer && !self.activePlayer.paused && !self.activePlayer.ended && !self.useNativeProgressMode) {
                self.progressRafId = requestAnimationFrame(loop);
            }
        };
        this.progressRafId = requestAnimationFrame(loop);
    },

    stopProgressLoop: function () {
        if (this.progressRafId) {
            cancelAnimationFrame(this.progressRafId);
            this.progressRafId = null;
        }
    },

    seekProgressTo: function (time) {
        if (!Number.isFinite(time)) {
            return;
        }

        const max = parseFloat(this.progressBar.slider?.max || "100");
        const clamped = Math.max(0, Math.min(time, max));
        this.setProgress(clamped, max, true);

        if (this.useNativeProgressMode && this.dotNetHelper) {
            this.dotNetHelper.invokeMethodAsync("OnProgressSeek", clamped);
            return;
        }

        this.setCurrentTime(clamped);
    },

    reportTimeToDotNet: function (currentTime) {
        if (!this.dotNetHelper) {
            return;
        }

        const now = Date.now();
        if (now - this.lastDotNetTimeReport < this.dotnetTimeIntervalMs) {
            return;
        }

        this.lastDotNetTimeReport = now;
        this.dotNetHelper.invokeMethodAsync("OnTimeUpdate", currentTime);
    },

    bindEvents: function (player) {
        if (!player) return;
        const self = window.audioPlayer;

        player.ontimeupdate = function () {
            if (self.isSeeking) return;
            if (self.activePlayer !== player) return;

            self.updateProgressFromPlayer();
            if (typeof player.currentTime === "number") {
                self.reportTimeToDotNet(player.currentTime);
            }
        };
        player.onseeked = function () {
            self.isSeeking = false;
            if (self.activePlayer === player) {
                self.updateProgressFromPlayer();
            }
        };
        player.onloadedmetadata = function () {
            if (self.activePlayer === player && self.dotNetHelper) {
                const d = Number.isFinite(player.duration) && player.duration > 0 ? player.duration : 100;
                self.setProgress(player.currentTime || 0, d, true);
                self.dotNetHelper.invokeMethodAsync('OnLoadedMetadata', d);
            }
        };
        player.onended = function () {
            self.stopProgressLoop();
            if (self.activePlayer === player && self.dotNetHelper) {
                self.dotNetHelper.invokeMethodAsync('OnEnded');
            }
        };
        player.onplay = function () {
            if (self.activePlayer !== player) {
                return;
            }

            if (player === self.nativeVideo && self.nativeVideo.muted && self.useNativeProgressMode) {
                return;
            }

            self.startProgressLoop();
            if (self.dotNetHelper) {
                self.dotNetHelper.invokeMethodAsync('OnPlayStateChanged', true);
            }
        };
        player.onpause = function () {
            if (self.activePlayer !== player) {
                return;
            }

            if (player === self.nativeVideo && self.nativeVideo.muted && self.useNativeProgressMode) {
                return;
            }

            self.stopProgressLoop();
            self.updateProgressFromPlayer();
            if (self.dotNetHelper) {
                self.dotNetHelper.invokeMethodAsync('OnPlayStateChanged', false);
            }
        };
        player.onerror = function () {
            self.reportError("media.onerror", player, player.error || new Error("media element error"));
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

    stopWebAudio: function () {
        if (!this.nativeAudio) {
            return;
        }

        this.nativeAudio.pause();
        this.nativeAudio.removeAttribute("src");
        this.nativeAudio.load();
    },

    pauseAllWebMedia: function () {
        if (this.nativeAudio) {
            this.nativeAudio.pause();
        }
        if (this.nativeVideo) {
            this.nativeVideo.pause();
        }
        this.stopProgressLoop();
    },

    stopWebPlayback: function () {
        this.pendingWebVideoShouldPlay = false;
        this.pendingWebVideoTime = null;
        this.clearWebVideoCanPlayHandler();
        this.stopWebAudio();
        if (this.nativeVideo) {
            this.nativeVideo.pause();
        }
        this.stopProgressLoop();
    },

    clearWebVideoCanPlayHandler: function () {
        if (!this.nativeVideo || !this.webVideoCanPlayHandler) {
            return;
        }

        this.nativeVideo.removeEventListener("canplay", this.webVideoCanPlayHandler);
        this.webVideoCanPlayHandler = null;
    },

    applyPendingWebVideoPlayback: function () {
        const video = this.nativeVideo;
        if (!video || !this.isVideoActive) {
            return;
        }

        const self = this;
        const run = function () {
            if (!self.isVideoActive || !self.nativeVideo) {
                return;
            }

            if (self.pendingWebVideoTime != null && Number.isFinite(self.pendingWebVideoTime)) {
                try {
                    video.currentTime = self.pendingWebVideoTime;
                } catch {
                    video.currentTime = self.pendingWebVideoTime;
                }
            }

            if (self.pendingWebVideoShouldPlay) {
                video.play().catch(function (e) {
                    self.reportError("applyPendingWebVideoPlayback", video, e);
                });
            }
        };

        if (video.readyState >= 2) {
            run();
            return;
        }

        this.clearWebVideoCanPlayHandler();
        this.webVideoCanPlayHandler = function () {
            self.clearWebVideoCanPlayHandler();
            run();
        };
        video.addEventListener("canplay", this.webVideoCanPlayHandler);
    },

    syncWebVideoState: function (url, isWebM, hybrid, playing, time) {
        if (!url || !this.nativeVideo) {
            this.log("syncWebVideoState skipped", { hasUrl: !!url, hasVideo: !!this.nativeVideo });
            return;
        }

        const video = this.nativeVideo;
        this.stopWebAudio();
        const currentSrc = video.currentSrc || video.src || "";
        const sameUrl = currentSrc === url;

        this.log("syncWebVideoState", {
            url: url,
            hybrid: !!hybrid,
            playing: !!playing,
            time: time,
            sameUrl: sameUrl
        });

        this.pendingUrl = url;
        this.pendingIsWebM = !!isWebM;
        this.pendingIsVideo = true;
        this.isVideoActive = true;
        this.activePlayer = video;
        this.pendingWebVideoShouldPlay = !!playing;
        this.pendingWebVideoTime = Number.isFinite(time) ? time : null;
        video.muted = !!hybrid;
        this.updateVideoVisibility();

        if (!sameUrl) {
            video.pause();
            video.src = url;
            video.load();
        } else if (!playing) {
            video.pause();
        }

        this.applyPendingWebVideoPlayback();
    },

    getFullscreenElement: function () {
        return document.fullscreenElement
            || document.webkitFullscreenElement
            || document.mozFullScreenElement
            || document.msFullscreenElement
            || null;
    },

    isVideoFullscreen: function () {
        const active = this.getFullscreenElement();
        if (!active) {
            return false;
        }

        if (this.fullscreenTarget) {
            return active === this.fullscreenTarget || this.fullscreenTarget.contains(active);
        }

        return active === this.nativeVideo;
    },

    notifyFullscreenChanged: function () {
        if (!this.fullscreenHelper) {
            return;
        }

        this.fullscreenHelper.invokeMethodAsync("OnVideoFullscreenChanged", this.isVideoFullscreen());
    },

    bindFullscreenEvents: function () {
        if (this.fullscreenEventsBound) {
            return;
        }

        this.fullscreenEventsBound = true;
        const self = window.audioPlayer;
        const handler = function () {
            self.notifyFullscreenChanged();
        };
        document.addEventListener("fullscreenchange", handler);
        document.addEventListener("webkitfullscreenchange", handler);
    },

    registerFullscreenListener: function (helper) {
        this.fullscreenHelper = helper;
        this.bindFullscreenEvents();
    },

    unregisterFullscreenListener: function () {
        this.fullscreenHelper = null;
    },

    attachVideoTo: function (container) {
        if (!this.nativeVideo || !container) {
            this.log("attachVideoTo skipped", { hasVideo: !!this.nativeVideo, hasContainer: !!container });
            return;
        }
        if (this.nativeVideo.parentElement !== container) {
            container.appendChild(this.nativeVideo);
        }
        this.fullscreenTarget = container.closest
            ? (container.closest(".player-video-frame") || container)
            : container;
        this.nativeVideo.controls = false;
        this.nativeVideo.classList.add("player-video");
        this.nativeVideo.style.display = "block";
        this.updateVideoVisibility();
        this.bindFullscreenEvents();
        this.log("attachVideoTo ok", {
            src: this.nativeVideo.currentSrc || this.nativeVideo.src || "",
            parent: this.nativeVideo.parentElement ? this.nativeVideo.parentElement.className : ""
        });

        if (this.isVideoActive) {
            this.applyPendingWebVideoPlayback();
        }
    },

    toggleVideoFullscreen: async function () {
        const target = this.fullscreenTarget || this.nativeVideo;
        if (!target) {
            this.log("toggleVideoFullscreen skipped", { hasTarget: false });
            return false;
        }

        try {
            if (this.isVideoFullscreen()) {
                if (document.exitFullscreen) {
                    await document.exitFullscreen();
                } else if (document.webkitExitFullscreen) {
                    document.webkitExitFullscreen();
                }
                return false;
            }

            if (target.requestFullscreen) {
                await target.requestFullscreen();
            } else if (target.webkitRequestFullscreen) {
                target.webkitRequestFullscreen();
            } else if (this.nativeVideo && this.nativeVideo.webkitEnterFullscreen) {
                this.nativeVideo.webkitEnterFullscreen();
            } else {
                throw new Error("Fullscreen API is not supported");
            }

            return true;
        } catch (e) {
            this.reportError("toggleVideoFullscreen", this.nativeVideo || target, e);
            return false;
        }
    },

    exitVideoFullscreen: async function () {
        if (!this.isVideoFullscreen()) {
            return;
        }

        try {
            if (document.exitFullscreen) {
                await document.exitFullscreen();
            } else if (document.webkitExitFullscreen) {
                document.webkitExitFullscreen();
            }
        } catch (e) {
            this.reportError("exitVideoFullscreen", this.nativeVideo, e);
        }
    },

    detachVideo: function () {
        this.exitVideoFullscreen();
        if (!this.nativeVideo || !this.videoHost) return;
        this.nativeVideo.pause();
        if (this.nativeVideo.parentElement !== this.videoHost) {
            this.videoHost.appendChild(this.nativeVideo);
        }
        this.nativeVideo.style.display = "none";
        this.fullscreenTarget = null;
    },

    clearVideoElement: function () {
        if (!this.nativeVideo) return;
        this.pendingWebVideoShouldPlay = false;
        this.pendingWebVideoTime = null;
        this.clearWebVideoCanPlayHandler();
        if (this.activePlayer === this.nativeVideo) {
            this.activePlayer.pause();
            this.activePlayer.src = "";
            this.activePlayer.load();
            this.activePlayer = this.nativeAudio;
        }
        this.isVideoActive = false;
        this.detachVideo();
    },

    localProxyFileKey: function (url) {
        if (!url) return "";
        try {
            const parsed = new URL(url, window.location.origin);
            return parsed.searchParams.get("f") || "";
        } catch {
            return "";
        }
    },

    loadSource: function (url, isWebM, isVideo) {
        if (!url) return;

        const nextIsVideo = !!isVideo;
        const currentSrc = this.activePlayer && (this.activePlayer.currentSrc || this.activePlayer.src);
        const currentFileKey = this.localProxyFileKey(currentSrc || "");
        const nextFileKey = this.localProxyFileKey(url);

        this.pendingUrl = url;
        this.pendingIsWebM = !!isWebM;
        this.pendingIsVideo = nextIsVideo;
        this.isVideoActive = nextIsVideo;
        this.updateVideoVisibility();

        if (!nextIsVideo) {
            this.pendingWebVideoShouldPlay = false;
            if (this.nativeVideo) {
                this.nativeVideo.pause();
                this.nativeVideo.muted = true;
                this.nativeVideo.removeAttribute("src");
                this.nativeVideo.load();
            }
        }

        if (!this.nativeVideo && this.isVideoActive) {
            return;
        }

        if (currentSrc && url === currentSrc && this.isVideoActive === nextIsVideo) {
            return;
        }

        // 本地同一文件：用重新 load（与下一首一致），不用 currentTime seek（webm 在 WebView2 里很慢）
        if (nextFileKey && currentFileKey && nextFileKey === currentFileKey && this.isVideoActive === nextIsVideo) {
            this.replayFromStart(url, isWebM, isVideo);
            return;
        }

        const isLocalFileSwitch = nextFileKey && currentFileKey && nextFileKey !== currentFileKey;

        this.isSeeking = false;
        this.stopProgressLoop();

        if (this.isVideoActive && this.nativeVideo) {
            this.activePlayer = this.nativeVideo;
            this.stopWebAudio();
            this.nativeVideo.muted = false;
        } else {
            this.activePlayer = this.nativeAudio;
        }

        if (this.activePlayer) {
            this.activePlayer.pause();
            if (!isLocalFileSwitch) {
                this.activePlayer.src = "";
                this.activePlayer.load();
            }
            this.activePlayer.src = url;
            if (isLocalFileSwitch) {
                this.activePlayer.load();
            }
        }

        this.setProgress(0, 100, true);
    },

    replayFromStart: function (url, isWebM, isVideo) {
        if (!url) {
            return;
        }

        const nextIsVideo = !!isVideo;
        this.pendingUrl = url;
        this.pendingIsWebM = !!isWebM;
        this.pendingIsVideo = nextIsVideo;
        this.isVideoActive = nextIsVideo;
        this.updateVideoVisibility();

        if (!this.nativeVideo && this.isVideoActive) {
            return;
        }

        this.isSeeking = false;
        this.stopProgressLoop();

        if (this.isVideoActive && this.nativeVideo) {
            this.activePlayer = this.nativeVideo;
        } else {
            this.activePlayer = this.nativeAudio;
        }

        if (this.activePlayer) {
            this.activePlayer.pause();
            this.activePlayer.src = url;
            this.activePlayer.load();
            this.setProgress(0, 100, true);
            this.play();
        }
    },

    loadAndPlay: function (url, isWebM, isVideo) {
        this.loadSource(url, isWebM, isVideo);
        this.play();
    },

    loadVideoOnly: function (url, isWebM, playing, time) {
        this.syncWebVideoState(url, isWebM, true, !!playing, Number.isFinite(time) ? time : null);
    },

    playVideoOnly: function () {
        if (!this.nativeVideo) {
            this.log("playVideoOnly skipped", { hasVideo: false });
            return;
        }

        this.pendingWebVideoShouldPlay = true;
        this.applyPendingWebVideoPlayback();
    },

    play: function () {
        if (this.isVideoActive && this.nativeVideo && this.activePlayer === this.nativeVideo) {
            this.pendingWebVideoShouldPlay = true;
            this.applyPendingWebVideoPlayback();
            return;
        }

        if (this.activePlayer) {
            this.activePlayer.play().catch(e => this.reportError("play", this.activePlayer, e));
        }
    },
    pause: function () {
        if (this.isVideoActive && this.nativeVideo) {
            this.pendingWebVideoShouldPlay = false;
            this.nativeVideo.pause();
            return;
        }

        if (this.activePlayer) {
            this.activePlayer.pause();
        }
    },
    setCurrentTime: function (time) {
        if (!this.activePlayer) {
            return;
        }

        const max = parseFloat(this.progressBar.slider?.max || "100");
        const clamped = Math.max(0, Math.min(time, max));
        this.setProgress(clamped, max, true);
        this.isSeeking = true;

        try {
            if (typeof this.activePlayer.fastSeek === "function") {
                this.activePlayer.fastSeek(clamped);
            } else {
                this.activePlayer.currentTime = clamped;
            }
        } catch {
            this.activePlayer.currentTime = clamped;
        }

        if (this.seekTimer) {
            clearTimeout(this.seekTimer);
        }
        this.seekTimer = setTimeout(() => { this.isSeeking = false; }, 600);

        if (!this.activePlayer.paused) {
            this.startProgressLoop();
        }
    },
    dispose: function () {
        this.stopProgressLoop();
        this.exitVideoFullscreen();
        this.unregisterFullscreenListener();
        this.stopWebPlayback();
        this.dotNetHelper = null;
    }
};
