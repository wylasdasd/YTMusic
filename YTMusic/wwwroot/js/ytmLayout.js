window.ytmLayout = window.ytmLayout || {};

window.ytmLayout.initBottomNav = function (navId) {
    const nav = document.getElementById(navId);
    if (!nav) {
        return;
    }

    if (nav.dataset.ytmBound === "1") {
        return;
    }
    nav.dataset.ytmBound = "1";

    const updateBottom = () => {
        const vv = window.visualViewport;
        if (!vv) {
            nav.style.bottom = "0px";
            return;
        }

        // Keyboard height approximation relative to layout viewport.
        const keyboardOffset = Math.max(0, window.innerHeight - (vv.height + vv.offsetTop));
        nav.style.bottom = `${keyboardOffset}px`;
    };

    updateBottom();

    window.addEventListener("resize", updateBottom, { passive: true });
    window.addEventListener("orientationchange", updateBottom, { passive: true });
    window.addEventListener("focusin", () => setTimeout(updateBottom, 0), { passive: true });
    window.addEventListener("focusout", () => setTimeout(updateBottom, 120), { passive: true });

    if (window.visualViewport) {
        window.visualViewport.addEventListener("resize", updateBottom, { passive: true });
        window.visualViewport.addEventListener("scroll", updateBottom, { passive: true });
    }
};
