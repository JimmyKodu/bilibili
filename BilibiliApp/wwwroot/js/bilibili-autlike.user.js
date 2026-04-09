// ==UserScript==
// @name         B站自动点赞
// @namespace    https://github.com/JimmyKodu/bilibili
// @version      1.0
// @description  打开B站视频页时自动点赞（需已在浏览器中登录B站）
// @author       BilibiliApp
// @match        https://www.bilibili.com/video/*
// @grant        none
// @run-at       document-idle
// ==/UserScript==

(function () {
    'use strict';

    function getCookie(name) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : null;
    }

    function getBvid() {
        var m = location.pathname.match(/\/BV([A-Za-z0-9]+)/i);
        return m ? 'BV' + m[1] : null;
    }

    function showToast(msg, color) {
        var el = document.createElement('div');
        el.textContent = msg;
        el.style.cssText = [
            'position:fixed', 'bottom:80px', 'right:24px', 'z-index:99999',
            'background:' + (color || '#23ade5'), 'color:#fff', 'border-radius:8px',
            'padding:10px 18px', 'font-size:14px', 'box-shadow:0 2px 12px rgba(0,0,0,.3)',
            'transition:opacity .5s', 'opacity:1', 'pointer-events:none'
        ].join(';');
        document.body.appendChild(el);
        setTimeout(function () {
            el.style.opacity = '0';
            setTimeout(function () { el.remove(); }, 600);
        }, 3000);
    }

    function doLike() {
        var csrf = getCookie('bili_jct');
        var bvid = getBvid();
        if (!csrf || !bvid) return;

        fetch('https://api.bilibili.com/x/web-interface/archive/like', {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: 'bvid=' + bvid + '&like=1&csrf=' + csrf
        })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.code === 0) {
                showToast('\u{1F44D} \u5df2\u81ea\u52a8\u70b9\u8d5e\uff01'); // 👍 已自动点赞！
            } else if (d.code === 65006) {
                // Already liked — silent, no notification needed
            } else {
                showToast('\u70b9\u8d5e\u5931\u8d25: ' + d.message, '#e53935'); // 点赞失败
            }
        })
        .catch(function (e) {
            console.error('[B\u7ad9\u81ea\u52a8\u70b9\u8d5e]', e);
        });
    }

    // Wait 2.5 s for the page to finish loading and cookies to be available
    setTimeout(doLike, 2500);
})();
