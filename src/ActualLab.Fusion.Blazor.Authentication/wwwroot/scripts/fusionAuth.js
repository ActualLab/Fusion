(function() {

    let encode = encodeURIComponent;

    window.FusionAuth = {
        schemas: "",
        windowTarget: "_blank",
        windowFeatures: "width=600,height=600",
        signInPath: "/signIn",
        signOutPath: "/signOut",
        closePath: "/fusion/close",
        enablePopup: true,
        mustRedirectOnPopupBlock: true,

        signIn: function(schema) {
            if (schema === undefined || schema === null || schema === "") {
                this.authPopupOrRedirect(this.signInPath, "Sign-in");
            } else {
                this.authPopupOrRedirect(this.signInPath + "/" + schema, "Sign-in");
            }
        },

        signOut: function() {
            this.authPopupOrRedirect(this.signOutPath, "Sign-out");
        },

        authPopupOrRedirect: function(action, flowName) {
            if (!this.enablePopup) {
                this.authRedirect(action);
                return;
            }

            let redirectUrl = this.toSiteRelativeUrl(this.closePath + "?flow=" + encode(flowName));
            let url = action + "?returnUrl=" + encode(redirectUrl);
            let popup = window.open(url, FusionAuth.windowTarget, FusionAuth.windowFeatures);
            if (!popup || popup.closed || typeof popup.closed == 'undefined') {
                if (this.mustRedirectOnPopupBlock) {
                    this.authRedirect(action);
                }
                else {
                    alert("Authentication popup is blocked by the browser. Please allow popups on this website and retry.")
                }
            }
        },

        authRedirect(action) {
            let redirectUrl = this.toSiteRelativeUrl(window.location.href);
            let url = action + "?returnUrl=" + encode(redirectUrl);
            window.location.replace(url);
        },

        // The server checks returnUrl with IUrlHelper.IsLocalUrl, which rejects absolute URLs - even
        // same-origin ones - and falls back to "/". baseURI keeps this right under a sub-path.
        toSiteRelativeUrl: function(url) {
            let parsed = new URL(url, document.baseURI);
            return parsed.pathname + parsed.search + parsed.hash;
        }
    };

})();
