// Wraps navigator.geolocation in a Promise so it can be awaited from Blazor
// via IJSRuntime.InvokeAsync. Resolves { latitude, longitude } on success;
// rejects with a short reason code the C# side maps to a friendly message.
export function getCurrentPosition() {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject("unsupported");
            return;
        }

        // getCurrentPosition's own `timeout` option only starts counting once
        // permission has been granted - while the browser's native permission
        // prompt is pending (easy to miss, shown outside the page), there is
        // no timeout at all and the call can hang indefinitely. This guard
        // timeout guarantees the promise always settles.
        let settled = false;
        const guardTimeoutId = setTimeout(() => {
            if (!settled) {
                settled = true;
                reject("timeout");
            }
        }, 20000);

        navigator.geolocation.getCurrentPosition(
            position => {
                if (settled) return;
                settled = true;
                clearTimeout(guardTimeoutId);
                resolve({
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude,
                });
            },
            error => {
                if (settled) return;
                settled = true;
                clearTimeout(guardTimeoutId);
                switch (error.code) {
                    case error.PERMISSION_DENIED:
                        reject("denied");
                        break;
                    case error.TIMEOUT:
                        reject("timeout");
                        break;
                    default:
                        reject("unavailable");
                }
            },
            { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
    });
}
