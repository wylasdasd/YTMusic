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

window.ytmLayout._pageScrolls = window.ytmLayout._pageScrolls || {};

window.ytmLayout._bindPageScrollRegion = function (element) {
    if (!element || element.dataset.ytmScrollBound === "1") {
        return;
    }

    const pageKey = element.dataset.page;
    if (!pageKey) {
        return;
    }

    element.dataset.ytmScrollBound = "1";
    element.addEventListener(
        "scroll",
        () => {
            window.ytmLayout._pageScrolls[pageKey] = element.scrollTop;
        },
        { passive: true }
    );
};

window.ytmLayout._scanPageScrollRegions = function () {
    document.querySelectorAll(".ytm-page__scroll[data-page]").forEach((element) => {
        window.ytmLayout._bindPageScrollRegion(element);
    });

    document.querySelectorAll(".ytm-page--tabs .mud-tab-panel[data-page]").forEach((element) => {
        window.ytmLayout._bindPageScrollRegion(element);
    });
};

window.ytmLayout.saveAllPageScrolls = function () {
    window.ytmLayout._scanPageScrollRegions();
    document.querySelectorAll(".ytm-page__scroll[data-page], .ytm-page--tabs .mud-tab-panel[data-page]").forEach((element) => {
        const pageKey = element.dataset.page;
        if (pageKey) {
            window.ytmLayout._pageScrolls[pageKey] = element.scrollTop;
        }
    });
};

window.ytmLayout.restorePageScrolls = function (retries) {
    const maxRetries = typeof retries === "number" ? retries : 10;
    if (maxRetries <= 0) {
        return;
    }

    window.ytmLayout._scanPageScrollRegions();

    let needsRetry = false;
    document.querySelectorAll(".ytm-page__scroll[data-page], .ytm-page--tabs .mud-tab-panel[data-page]").forEach((element) => {
        const pageKey = element.dataset.page;
        if (!pageKey) {
            return;
        }

        const target = window.ytmLayout._pageScrolls[pageKey] ?? 0;
        element.scrollTop = target;
        if (Math.abs(element.scrollTop - target) > 2) {
            needsRetry = true;
        }
    });

    if (needsRetry) {
        setTimeout(() => window.ytmLayout.restorePageScrolls(maxRetries - 1), 60);
    }
};

window.ytmLayout.initPageScrollPersistence = function () {
    const body = document.querySelector(".ytm-body");
    if (!body || body.dataset.ytmPageScrollInit === "1") {
        window.ytmLayout._scanPageScrollRegions();
        return;
    }

    body.dataset.ytmPageScrollInit = "1";
    window.ytmLayout._scanPageScrollRegions();

    const observer = new MutationObserver(() => {
        window.ytmLayout._scanPageScrollRegions();
    });
    observer.observe(body, { childList: true, subtree: true });
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
