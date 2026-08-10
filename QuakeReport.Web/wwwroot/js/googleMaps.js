// Thin wrapper around the Google Maps JS API (Places Autocomplete + Geocoder).
// The API key is a browser key by necessity - it has to load in the page for
// the widget to work. Restrict it by HTTP referrer in Google Cloud Console;
// that's the real protection, not keeping it out of the page source.

let loadPromise = null;

function loadScript(apiKey) {
    if (window.google?.maps?.places) {
        return Promise.resolve();
    }
    if (loadPromise) {
        return loadPromise;
    }

    loadPromise = new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places`;
        script.async = true;
        script.onload = () => resolve();
        script.onerror = () => reject("Failed to load Google Maps script.");
        document.head.appendChild(script);
    });

    return loadPromise;
}

// Attaches Places Autocomplete to the <input> found inside wrapperElement
// (MudTextField doesn't expose its inner <input> as an ElementReference, so
// the wrapping div is passed in and the actual input is looked up here).
// When a place is picked, calls back into .NET with the formatted address
// and its coordinates.
export async function attachAutocomplete(apiKey, wrapperElement, dotNetRef) {
    await loadScript(apiKey);

    const inputElement = wrapperElement.querySelector("input");
    if (!inputElement) {
        return;
    }

    const autocomplete = new google.maps.places.Autocomplete(inputElement, {
        fields: ["formatted_address", "geometry"],
    });

    autocomplete.addListener("place_changed", () => {
        const place = autocomplete.getPlace();
        if (!place.geometry?.location) {
            return;
        }
        dotNetRef.invokeMethodAsync(
            "OnPlaceSelectedFromJs",
            place.formatted_address ?? inputElement.value,
            place.geometry.location.lat(),
            place.geometry.location.lng());
    });
}

// Best-effort reverse geocode, used to pre-fill the address box from GPS.
export async function reverseGeocode(apiKey, latitude, longitude) {
    await loadScript(apiKey);

    return new Promise(resolve => {
        const geocoder = new google.maps.Geocoder();
        geocoder.geocode({ location: { lat: latitude, lng: longitude } }, (results, status) => {
            if (status === "OK" && results?.[0]) {
                resolve(results[0].formatted_address);
            } else {
                resolve(null);
            }
        });
    });
}
