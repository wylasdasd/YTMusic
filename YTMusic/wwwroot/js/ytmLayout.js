window.ytmLayout = window.ytmLayout || {};

window.ytmLayout._scrollActiveTabIntoView = function (root) {
    if (!root) {
        return;
    }

    const content = root.querySelector(".mud-tabs-tabbar-content");
    const active = root.querySelector(".mud-tab.mud-tab-active");
    if (!content || !active) {
        return;
    }

    const padding = 8;
    const tabLeft = active.offsetLeft;
    const tabRight = tabLeft + active.offsetWidth;
    const scrollLeft = content.scrollLeft;
    const viewWidth = content.clientWidth;

    if (tabLeft < scrollLeft) {
        content.scrollTo({ left: Math.max(0, tabLeft - padding), behavior: "smooth" });
    } else if (tabRight > scrollLeft + viewWidth) {
        content.scrollTo({ left: tabRight - viewWidth + padding, behavior: "smooth" });
    }
};

window.ytmLayout.initTouchScrollTabs = function (rootId) {
    const root = document.getElementById(rootId);
    if (!root) {
        return;
    }

    if (root.dataset.ytmTouchTabs === "1") {
        window.ytmLayout._scrollActiveTabIntoView(root);
        return;
    }

    root.dataset.ytmTouchTabs = "1";

    const tabbar = root.querySelector(".mud-tabs-tabbar-inner");
    if (tabbar) {
        const observer = new MutationObserver(() => {
            requestAnimationFrame(() => window.ytmLayout._scrollActiveTabIntoView(root));
        });
        observer.observe(tabbar, {
            attributes: true,
            subtree: true,
            attributeFilter: ["class"]
        });
    }

    window.ytmLayout._scrollActiveTabIntoView(root);
};

window.ytmLayout.syncTouchScrollTabs = function (rootId) {
    const root = document.getElementById(rootId);
    window.ytmLayout._scrollActiveTabIntoView(root);
};

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
