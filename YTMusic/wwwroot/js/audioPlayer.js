window.audioPlayer = {
    dotNetHelper: null,
    nativeAudio: null,
    ogvPlayer: null,
    activePlayer: null,
    isSeeking: false,
    seekTimer: null,

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
        this.activePlayer.play().catch(e => console.error("Play error:", e));
    },

    play: function () {
        if (this.activePlayer) {
            this.activePlayer.play().catch(e => console.error("Play error:", e));
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
