window.helpSharing = {
    async share: async function (data) {
        if (navigator.share) {
            try {
                await navigator.share({ title: data.title, text: data.text, url: data.url });
                return "Shared";
            } catch (error) {
                if (error && error.name === "AbortError") return "Cancelled";
            }
        }

        try {
            await navigator.clipboard.writeText(data.url);
            return "Copied";
        } catch {
            const input = document.createElement("textarea");
            input.value = data.url;
            input.setAttribute("readonly", "");
            input.style.position = "fixed";
            input.style.opacity = "0";
            document.body.appendChild(input);
            input.select();
            const copied = document.execCommand("copy");
            input.remove();
            return copied ? "Copied" : "Failed";
        }
    }
};
