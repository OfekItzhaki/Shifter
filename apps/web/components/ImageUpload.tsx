"use client";

import { useRef, useState } from "react";
import { uploadImage } from "@/lib/api/uploads";

interface ImageUploadProps {
  /** Current image URL (shown as preview) */
  value?: string | null;
  /** Called with the new public URL after a successful upload */
  onChange: (url: string) => void;
  /** Shape of the preview: "circle" for avatars, "square" for logos/banners */
  shape?: "circle" | "square";
  /** Size in pixels for the preview area */
  size?: number;
  /** Placeholder label shown when no image is set */
  label?: string;
  disabled?: boolean;
}

const ACCEPTED = "image/jpeg,image/png,image/webp,image/gif";

export default function ImageUpload({
  value,
  onChange,
  shape = "circle",
  size = 80,
  label = "Upload photo",
  disabled = false,
}: ImageUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const borderRadius = shape === "circle" ? "50%" : 12;

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    // Client-side size check (10 MB)
    if (file.size > 10 * 1024 * 1024) {
      setError("File too large. Maximum size is 10 MB.");
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
        "Upload failed. Please try again.";
      setError(msg);
    } finally {
      setUploading(false);
      // Reset input so the same file can be re-selected after an error
      if (inputRef.current) inputRef.current.value = "";
    }
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 8 }}>
      <button
        type="button"
        disabled={disabled || uploading}
        onClick={() => inputRef.current?.click()}
        style={{
          width: size,
          height: size,
          borderRadius,
          border: "2px dashed #cbd5e1",
          background: "#f8fafc",
          cursor: disabled || uploading ? "not-allowed" : "pointer",
          overflow: "hidden",
          position: "relative",
          padding: 0,
          flexShrink: 0,
        }}
        aria-label={label}
      >
        {value ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={value}
            alt="Preview"
            style={{ width: "100%", height: "100%", objectFit: "cover" }}
          />
        ) : (
          <span style={{ fontSize: 11, color: "#94a3b8", padding: 4, display: "block", textAlign: "center" }}>
            {uploading ? "Uploading…" : label}
          </span>
        )}

        {/* Hover overlay */}
        {!disabled && (
          <span
            style={{
              position: "absolute",
              inset: 0,
              background: "rgba(0,0,0,0.35)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              opacity: 0,
              transition: "opacity 0.15s",
              borderRadius,
            }}
            className="upload-overlay"
          >
            <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="white" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
            </svg>
          </span>
        )}
      </button>

      <input
        ref={inputRef}
        type="file"
        accept={ACCEPTED}
        style={{ display: "none" }}
        onChange={handleFileChange}
        disabled={disabled || uploading}
      />

      {uploading && (
        <span style={{ fontSize: 12, color: "#64748b" }}>Uploading…</span>
      )}

      {error && (
        <span style={{ fontSize: 12, color: "#dc2626", maxWidth: size * 2, textAlign: "center" }}>
          {error}
        </span>
      )}

      <style>{`
        button:hover .upload-overlay { opacity: 1 !important; }
      `}</style>
    </div>
  );
}
