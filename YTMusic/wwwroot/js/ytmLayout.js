// YTMusic 布局相关 JS（由 index.html 引入，Blazor 通过 IJSRuntime 调用）。
window.ytmLayout = window.ytmLayout || {};

// ---------------------------------------------------------------------------
// Upload 页 MudTabs：横向触摸滑动（隐藏 MudBlazor 左右箭头，用原生 overflow 滚动）
// 配合 app.css 中 .ytm-tabs-touch-scroll；Upload.razor 调用 initTouchScrollTabs / syncTouchScrollTabs
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// 各页面列表滚动位置缓存（Blazor 路由会销毁 DOM，无法“自带”保留 scrollTop）
//
// 存储：window.ytmLayout._pageScrolls[pageKey] = scrollTop（仅内存，刷新应用即丢失）
// 目标元素：带 data-page 的 .ytm-page__scroll（PageListScroll）或 Upload 的 MudTabPanel
// C# 调用点：MainLayout OnAfterRender → initPageScrollPersistence / restorePageScrolls
//           底栏 NavigateToTabAsync → saveAllPageScrolls（跳转前再扫一遍 DOM）
// ---------------------------------------------------------------------------

window.ytmLayout._pageScrolls = window.ytmLayout._pageScrolls || {};

/** 给单个滚动容器绑定 scroll 监听，滚动时写入 _pageScrolls[pageKey] */
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

/** 扫描当前 DOM 中所有带 data-page 的列表滚动区并绑定监听 */
window.ytmLayout._scanPageScrollRegions = function () {
    document.querySelectorAll(".ytm-page__scroll[data-page]").forEach((element) => {
        window.ytmLayout._bindPageScrollRegion(element);
    });

    document.querySelectorAll(".ytm-page--tabs .mud-tab-panel[data-page]").forEach((element) => {
        window.ytmLayout._bindPageScrollRegion(element);
    });
};

/** 跳转前显式保存（底栏切换时由 MainLayout 调用） */
window.ytmLayout.saveAllPageScrolls = function () {
    window.ytmLayout._scanPageScrollRegions();
    document.querySelectorAll(".ytm-page__scroll[data-page], .ytm-page--tabs .mud-tab-panel[data-page]").forEach((element) => {
        const pageKey = element.dataset.page;
        if (pageKey) {
            window.ytmLayout._pageScrolls[pageKey] = element.scrollTop;
        }
    });
};

/** 路由切换后恢复滚动；异步列表可能尚未撑满高度，故带有限次重试 */
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

/** 应用启动时绑定；MutationObserver 在 .ytm-body 子树变化时重新扫描（新页面挂载后） */
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

// ---------------------------------------------------------------------------
// 底部导航：键盘弹出时抬高 bottom，避免被输入法挡住（visualViewport）
// ---------------------------------------------------------------------------

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
