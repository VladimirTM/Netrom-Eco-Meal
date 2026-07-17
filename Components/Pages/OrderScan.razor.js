// First start/stop-lifecycle JS isolation module in this repo (ReconnectModal.razor.js is a
// static event-listener script, not a good template for this). Runs the entire camera capture
// -> canvas -> jsQR decode loop client-side with zero per-frame calls back into the Blazor
// Server circuit; a successful, validated decode does a real browser navigation instead.
import "/lib/jsqr/jsQR.js";

const VALIDATE_PATH_PATTERN = /^\/orders\/validate\/[0-9a-fA-F-]{36}$/;

let stream = null;
let rafId = null;

export async function startCamera(videoEl, canvasEl) {
    // Idempotent: a stray double-tap or a circuit reconnect while the camera is already open
    // should not request a second stream.
    stopCamera();

    stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: "environment" },
        audio: false,
    });

    videoEl.srcObject = stream;
    await videoEl.play();

    const canvasCtx = canvasEl.getContext("2d", { willReadFrequently: true });

    const tick = () => {
        if (!stream) return; // stopCamera() was called while a frame was in flight

        if (videoEl.readyState === videoEl.HAVE_ENOUGH_DATA) {
            canvasEl.width = videoEl.videoWidth;
            canvasEl.height = videoEl.videoHeight;
            canvasCtx.drawImage(videoEl, 0, 0, canvasEl.width, canvasEl.height);

            const imageData = canvasCtx.getImageData(0, 0, canvasEl.width, canvasEl.height);
            const code = window.jsQR(imageData.data, imageData.width, imageData.height, {
                inversionAttempts: "dontInvert",
            });

            if (code && code.data && tryNavigate(code.data)) {
                return; // matched — stop the loop, we're navigating away
            }
        }

        rafId = requestAnimationFrame(tick);
    };

    rafId = requestAnimationFrame(tick);
}

export function stopCamera() {
    if (rafId !== null) {
        cancelAnimationFrame(rafId);
        rafId = null;
    }
    if (stream) {
        stream.getTracks().forEach((track) => track.stop());
        stream = null;
    }
}

// A crafted QR code must not be able to redirect an authenticated manager's session off-app —
// only navigate for same-origin URLs shaped exactly like our validate route.
function tryNavigate(decoded) {
    let url;
    try {
        url = new URL(decoded, location.origin);
    } catch {
        return false;
    }

    if (url.origin !== location.origin || !VALIDATE_PATH_PATTERN.test(url.pathname)) {
        return false;
    }

    stopCamera();
    location.assign(url.href);
    return true;
}
