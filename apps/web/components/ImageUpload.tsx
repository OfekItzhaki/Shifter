"use client";

import { useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { uploadImage } from "@/lib/api/uploads";

interface ImageUploadProps {
  value?: string | null;
  onChange: (url: string) => void;
  shape?: "circle" | "square";
  size?: number;
  label?: string;
  disabled?: boolean;
}

const ACCEPTED = "image/jpeg,image/png,image/webp,image/gif";
const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB

/** Sanitize and validate an image URL entered by the user */
function sanitizeImageUrl(raw: string): { url: string; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) return { url: "", error: null };

  try {
    const parsed = new URL(trimmed);
    // Only allow http/https
    if (parsed.protocol !== "https:" && parsed.protocol !== "http:") {
      return { url: "", error: "Only http/https URLs are allowed." };
    }
    // Block obviously dangerous patterns
    if (parsed.hostname === "localhost" && process.env.NODE_ENV === "production") {
      return { url: "", error: "Localhost URLs are not allowed in production." };
    }
    return { url: parsed.toString(), error: null };
  } catch {
    return { url: "", error: "Invalid URL format." };
  }
}

export default function ImageUpload({
  value,
  onChange,
  shape = "circle",
  size = 80,
  label,
  disabled = false,
}: ImageUploadProps) {
  const t = useTranslations("imageUpload");
  const resolvedLabel = label ?? t("upload");
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [mode, setMode] = useState<"preview" | "url">("preview");
  const [urlInput, setUrlInput] = useState("");

  const borderRadius = shape === "circle" ? "50%" : 12;

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    if (file.size > MAX_FILE_SIZE_BYTES) {
      setError(t("fileTooLarge"));
      return;
    }
    setError(null);
    setUploading(true);
    try {
      const url = await uploadImage(file);
      onChange(url);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t("uploadFailed");
      setError(msg);
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  }

  function handleUrlSubmit() {
    const { url, error: urlError } = sanitizeImageUrl(urlInput);
    if (urlError) { setError(urlError); return; }
    if (!url) { setError(t("invalidUrl")); return; }
    setError(null);
    onChange(url);
    setMode("preview");
    setUrlInput("");
  }

  // URL input mode
  if (mode === "url") {
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 8, width: "100%", maxWidth: 320 }}>
        <div style={{ display: "flex", gap: 6 }}>
          <input
            type="url"
            value={urlInput}
            onChange={e => { setUrlInput(e.target.value); setError(null); }}
            placeholder="https://example.com/photo.jpg"
            style={{
              flex: 1, border: "1px solid #e2e8f0", borderRadius: 10,
              padding: "6px 10px", fontSize: 13, outline: "none",
            }}
            onKeyDown={e => e.key === "Enter" && handleUrlSubmit()}
            autoFocus
          />
          <button
            type="button"
            onClick={handleUrlSubmit}
            style={{
              background: "#0ea5e9", color: "white", border: "none",
              borderRadius: 10, padding: "6px 12px", fontSize: 13,
              fontWeight: 600, cursor: "pointer", flexShrink: 0,
            }}
          >
            {t("confirm")}
          </button>
          <button
            type="button"
            onClick={() => { setMode("preview"); setError(null); setUrlInput(""); }}
            style={{
              background: "none", border: "1px solid #e2e8f0", borderRadius: 10,
              padding: "6px 10px", fontSize: 13, color: "#64748b", cursor: "pointer",
            }}
          >
            {t("cancel")}
          </button>
        </div>
        {error && <span style={{ fontSize: 12, color: "#dc2626" }}>{error}</span>}
      </div>
    );
  }

  // Preview / upload mode
  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 8 }}>
      <button
        type="button"
        disabled={disabled || uploading}
        onClick={() => inputRef.current?.click()}
        style={{
          width: size, height: size, borderRadius,
          border: "2px dashed #cbd5e1", background: "#f8fafc",
          cursor: disabled || uploading ? "not-allowed" : "pointer",
          overflow: "hidden", position: "relative", padding: 0, flexShrink: 0,
        }}
        aria-label={resolvedLabel}
      >
        {value ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={value} alt={resolvedLabel} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
        ) : (
          <span style={{ fontSize: 11, color: "#94a3b8", padding: 4, display: "block", textAlign: "center" }}>
            {uploading ? t("uploading") : resolvedLabel}
          </span>
        )}
        {!disabled && (
          <span
            style={{
              position: "absolute", inset: 0, background: "rgba(0,0,0,0.35)",
              display: "flex", alignItems: "center", justifyContent: "center",
              opacity: 0, transition: "opacity 0.15s", borderRadius,
            }}
            className="upload-overlay"
          >
            <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="white" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
            </svg>
          </span>
        )}
      </button>

      <input ref={inputRef} type="file" accept={ACCEPTED} style={{ display: "none" }} onChange={handleFileChange} disabled={disabled || uploading} />

      {/* Action buttons */}
      {!disabled && (
        <div style={{ display: "flex", gap: 6 }}>
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            disabled={uploading}
            style={{
              fontSize: 11, color: "#0ea5e9", background: "none",
              border: "1px solid #bfdbfe", borderRadius: 8,
              padding: "3px 8px", cursor: "pointer",
            }}
          >
            {uploading ? t("uploading") : t("uploadFile")}
          </button>
          <button
            type="button"
            onClick={() => { setMode("url"); setError(null); }}
            style={{
              fontSize: 11, color: "#64748b", background: "none",
              border: "1px solid #e2e8f0", borderRadius: 8,
              padding: "3px 8px", cursor: "pointer",
            }}
          >
            {t("enterUrl")}
          </button>
          {value && (
            <button
              type="button"
              onClick={() => onChange("")}
              style={{
                fontSize: 11, color: "#dc2626", background: "none",
                border: "1px solid #fecaca", borderRadius: 8,
                padding: "3px 8px", cursor: "pointer",
              }}
            >
              {t("remove")}
            </button>
          )}
        </div>
      )}

      {error && (
        <span style={{ fontSize: 12, color: "#dc2626", maxWidth: size * 2, textAlign: "center" }}>
          {error}
        </span>
      )}

      <style>{`button:hover .upload-overlay { opacity: 1 !important; }`}</style>
    </div>
  );
}
