window.turnstileInterop = {
    render: function (element, siteKey, dotNetReference) {
        if (!window.turnstile || !element) return;
        window.turnstile.render(element, {
            sitekey: siteKey,
            callback: function (token) { dotNetReference.invokeMethodAsync('SetToken', token); },
            'expired-callback': function () { dotNetReference.invokeMethodAsync('ClearToken'); },
            'error-callback': function () { dotNetReference.invokeMethodAsync('ClearToken'); }
        });
    }
};
