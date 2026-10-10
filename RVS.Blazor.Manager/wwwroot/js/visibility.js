// Tab-visibility hook for NewBuildPrompt (issue #859). Tells .NET when the tab is shown or
// hidden, so a tab coming back to the foreground checks for a new build straight away and a
// hidden one stops polling.
(function () {
    "use strict";

    let handler = null;

    window.rvsVisibility_subscribe = function (dotNetRef) {
        window.rvsVisibility_unsubscribe();
        handler = function () {
            dotNetRef.invokeMethodAsync("OnVisibilityChanged", document.visibilityState === "visible")
                .catch(function () { /* component already disposed */ });
        };
        document.addEventListener("visibilitychange", handler);
    };

    window.rvsVisibility_unsubscribe = function () {
        if (handler) {
            document.removeEventListener("visibilitychange", handler);
            handler = null;
        }
    };
})();
