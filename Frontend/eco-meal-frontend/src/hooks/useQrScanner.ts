import jsQR from "jsqr";
import { useCallback, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";

const VALIDATE_PATH_PATTERN = /^\/orders\/validate\/[0-9a-fA-F-]{36}\/[0-9a-fA-F-]{36}$/;

// Ports OrderScan.razor.js — the entire capture → canvas → jsQR decode loop runs client-side; a
// successful, validated decode does a real client-side navigation via react-router.
export function useQrScanner(videoRef: React.RefObject<HTMLVideoElement | null>, canvasRef: React.RefObject<HTMLCanvasElement | null>) {
  const navigate = useNavigate();
  const [scanning, setScanning] = useState(false);
  const [starting, setStarting] = useState(false);
  const [cameraError, setCameraError] = useState<string | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const rafRef = useRef<number | null>(null);

  const stopCamera = useCallback(() => {
    if (rafRef.current !== null) {
      cancelAnimationFrame(rafRef.current);
      rafRef.current = null;
    }
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => track.stop());
      streamRef.current = null;
    }
    setScanning(false);
  }, []);

  // A crafted QR code must not be able to navigate an authenticated manager's session somewhere
  // unexpected — only same-origin URLs shaped exactly like our validate route are followed.
  const tryNavigate = useCallback(
    (decoded: string): boolean => {
      let url: URL;
      try {
        url = new URL(decoded, window.location.origin);
      } catch {
        return false;
      }

      if (url.origin !== window.location.origin || !VALIDATE_PATH_PATTERN.test(url.pathname)) return false;

      stopCamera();
      navigate(url.pathname + url.search);
      return true;
    },
    [navigate, stopCamera],
  );

  const startCamera = useCallback(async () => {
    setStarting(true);
    setCameraError(null);
    stopCamera();

    try {
      const videoEl = videoRef.current;
      const canvasEl = canvasRef.current;
      if (!videoEl || !canvasEl) throw new Error("Scanner isn't ready.");

      const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" }, audio: false });
      streamRef.current = stream;
      videoEl.srcObject = stream;
      await videoEl.play();

      const canvasCtx = canvasEl.getContext("2d", { willReadFrequently: true });
      if (!canvasCtx) throw new Error("Couldn't start the camera. Make sure this device has one and try again.");

      const tick = () => {
        if (!streamRef.current) return;

        if (videoEl.readyState === videoEl.HAVE_ENOUGH_DATA) {
          canvasEl.width = videoEl.videoWidth;
          canvasEl.height = videoEl.videoHeight;
          canvasCtx.drawImage(videoEl, 0, 0, canvasEl.width, canvasEl.height);

          const imageData = canvasCtx.getImageData(0, 0, canvasEl.width, canvasEl.height);
          const code = jsQR(imageData.data, imageData.width, imageData.height, { inversionAttempts: "dontInvert" });

          if (code?.data && tryNavigate(code.data)) return;
        }

        rafRef.current = requestAnimationFrame(tick);
      };

      rafRef.current = requestAnimationFrame(tick);
      setScanning(true);
    } catch (err) {
      const message = err instanceof Error ? err.message : "";
      setCameraError(
        /permission|notallowederror/i.test(message)
          ? "Camera access was denied. Allow camera access for this site in your browser settings and try again."
          : "Couldn't start the camera. Make sure this device has one and try again.",
      );
    } finally {
      setStarting(false);
    }
  }, [videoRef, canvasRef, tryNavigate, stopCamera]);

  return { scanning, starting, cameraError, startCamera, stopCamera };
}
