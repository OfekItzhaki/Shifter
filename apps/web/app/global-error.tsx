"use client";

/**
 * Next.js global error boundary — catches errors in the root layout itself.
 * This MUST include its own <html> and <body> tags since the root layout may have crashed.
 * Cannot use useTranslations or any provider since they live in the root layout.
 */
export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="he" dir="rtl">
      <body style={{ margin: 0, fontFamily: "system-ui, -apple-system, sans-serif" }}>
        <div
          style={{
            minHeight: "100vh",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            background: "#f8fafc",
            padding: "1rem",
          }}
        >
          <div style={{ textAlign: "center", maxWidth: 420 }}>
            {/* Logo */}
            <div
              style={{
                width: 48,
                height: 48,
                borderRadius: 12,
                background: "#3b82f6",
                display: "inline-flex",
                alignItems: "center",
                justifyContent: "center",
                marginBottom: 24,
              }}
            >
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
                <path
                  d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"
                  stroke="white"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
            </div>

            {/* Status */}
            <p
              style={{
                fontSize: "4.5rem",
                fontWeight: 800,
                color: "#e2e8f0",
                margin: "0 0 0.5rem",
                lineHeight: 1,
              }}
            >
              500
            </p>

            {/* Heading */}
            <h1
              style={{
                fontSize: "1.5rem",
                fontWeight: 600,
                color: "#0f172a",
                margin: "0 0 0.5rem",
              }}
            >
              משהו השתבש
            </h1>

            {/* Message */}
            <p
              style={{
                fontSize: "1rem",
                color: "#64748b",
                margin: "0 0 2rem",
                lineHeight: 1.6,
              }}
            >
              אירעה שגיאה בלתי צפויה. נסה לרענן את הדף או לחזור מאוחר יותר.
            </p>

            {/* Actions */}
            <div style={{ display: "flex", gap: 12, justifyContent: "center", flexWrap: "wrap" }}>
              <button
                onClick={() => reset()}
                style={{
                  padding: "10px 24px",
                  borderRadius: 12,
                  background: "#3b82f6",
                  color: "white",
                  border: "none",
                  fontSize: "0.875rem",
                  fontWeight: 500,
                  cursor: "pointer",
                  minHeight: 44,
                }}
              >
                נסה שוב
              </button>
              <a
                href="/"
                style={{
                  padding: "10px 24px",
                  borderRadius: 12,
                  background: "white",
                  color: "#334155",
                  border: "1px solid #e2e8f0",
                  fontSize: "0.875rem",
                  fontWeight: 500,
                  textDecoration: "none",
                  display: "inline-flex",
                  alignItems: "center",
                  minHeight: 44,
                }}
              >
                חזרה לדף הבית
              </a>
            </div>

            {/* Subtle error digest for debugging */}
            {error.digest && (
              <p
                style={{
                  marginTop: 32,
                  fontSize: "0.7rem",
                  color: "#94a3b8",
                  fontFamily: "monospace",
                }}
              >
                Error ID: {error.digest}
              </p>
            )}
          </div>
        </div>
      </body>
    </html>
  );
}
