"use client";

import { useState, useEffect, useRef } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useHasMounted } from "@/lib/hooks/useHasMounted";
import { getMySpaces, SpaceDto } from "@/lib/api/spaces";

export default function SpaceSwitcher() {
  const t = useTranslations("spaces");
  const router = useRouter();
  const queryClient = useQueryClient();
  const hasMounted = useHasMounted();
  const { currentSpaceId, currentSpaceName, setCurrentSpace, clearSpace } =
    useSpaceStore();
  const [spaces, setSpaces] = useState<SpaceDto[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [loadError, setLoadError] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!hasMounted) return;

    getMySpaces()
      .then((fetched) => {
        setLoadError(false);
        setSpaces(fetched);

        // Handle invalid persisted space: if the stored space is not in the
        // user's list, clear it and fall back to the first available space.
        if (fetched.length > 0) {
          const storedIsValid =
            currentSpaceId && fetched.some((s) => s.id === currentSpaceId);
          if (!storedIsValid) {
            clearSpace();
            setCurrentSpace(fetched[0].id, fetched[0].name);
          }
        } else {
          // User has no spaces — clear selection and redirect to onboarding
          clearSpace();
          router.replace("/onboarding");
        }
      })
      .catch(() => {
        setLoadError(true);
      });
    // Re-fetch when the active space changes (e.g. after creating a new one)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentSpaceId, hasMounted]);

  // Close dropdown on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  function handleSwitch(space: SpaceDto) {
    if (space.id === currentSpaceId) {
      setIsOpen(false);
      return;
    }
    // Update store
    setCurrentSpace(space.id, space.name);
    setIsOpen(false);

    // Invalidate all cached data so the new space's data is fetched fresh
    queryClient.invalidateQueries();

    // Refresh the current route to re-render with new space context
    router.refresh();
  }

  function handleCreateNew() {
    setIsOpen(false);
    router.push("/onboarding");
  }

  function handleRetry() {
    setLoadError(false);
    getMySpaces()
      .then((fetched) => {
        setSpaces(fetched);
        if (fetched.length > 0) {
          const storedIsValid =
            currentSpaceId && fetched.some((s) => s.id === currentSpaceId);
          if (!storedIsValid) {
            clearSpace();
            setCurrentSpace(fetched[0].id, fetched[0].name);
          }
        } else {
          clearSpace();
        }
      })
      .catch(() => {
        setLoadError(true);
      });
  }

  const mountedSpaceName = hasMounted ? currentSpaceName : null;
  const displayName = mountedSpaceName
    ? mountedSpaceName.length > 30
      ? mountedSpaceName.slice(0, 30) + "…"
      : mountedSpaceName
    : t("noSpace");

  return (
    <div ref={containerRef} style={{ position: "relative" }}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        style={{
          display: "flex",
          alignItems: "center",
          gap: 6,
          padding: "4px 8px",
          borderRadius: 6,
          background: "transparent",
          border: "none",
          cursor: "pointer",
          width: "100%",
          textAlign: "start",
        }}
        title={mountedSpaceName ?? undefined}
      >
        <svg
          width="14"
          height="14"
          fill="none"
          viewBox="0 0 24 24"
          stroke="var(--sidebar-muted)"
          strokeWidth={2}
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
          />
        </svg>
        <span
          style={{
            color: "var(--sidebar-muted)",
            fontSize: 11,
            flex: 1,
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          }}
        >
          {displayName}
        </span>
        <svg
          width="10"
          height="10"
          fill="none"
          viewBox="0 0 24 24"
          stroke="var(--sidebar-muted)"
          strokeWidth={2}
          style={{
            flexShrink: 0,
            transform: isOpen ? "rotate(180deg)" : "none",
            transition: "transform 120ms ease",
          }}
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M19 9l-7 7-7-7"
          />
        </svg>
      </button>

      {isOpen && (
        <div
          role="listbox"
          style={{
            position: "absolute",
            top: "100%",
            left: 0,
            right: 0,
            marginTop: 4,
            background: "var(--app-surface)",
            border: "1px solid var(--sidebar-border)",
            borderRadius: 10,
            boxShadow: "0 4px 12px rgba(0,0,0,0.3)",
            zIndex: 50,
            padding: 4,
            maxHeight: 200,
            overflowY: "auto",
          }}
        >
          {loadError ? (
            <div style={{ padding: "8px 10px", textAlign: "center" }}>
              <span style={{ color: "#f87171", fontSize: 12 }}>
                {t("noSpace")}
              </span>
              <button
                onClick={handleRetry}
                style={{
                  display: "block",
                  margin: "6px auto 0",
                  padding: "4px 10px",
                  borderRadius: 4,
                  border: "1px solid var(--sidebar-border)",
                  background: "transparent",
                  color: "#0ea5e9",
                  fontSize: 11,
                  cursor: "pointer",
                }}
              >
                ↻ Retry
              </button>
            </div>
          ) : (
            <>
              {spaces.map((space) => (
                <button
                  key={space.id}
                  role="option"
                  aria-selected={space.id === currentSpaceId}
                  onClick={() => handleSwitch(space)}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    width: "100%",
                    padding: "8px 10px",
                    borderRadius: 6,
                    border: "none",
                    background:
                      space.id === currentSpaceId
                        ? "rgba(14,165,233,0.15)"
                        : "transparent",
                    color:
                      space.id === currentSpaceId ? "var(--sidebar-link-active-fg)" : "var(--sidebar-muted)",
                    fontSize: 12,
                    fontWeight: space.id === currentSpaceId ? 600 : 400,
                    cursor: "pointer",
                    textAlign: "start",
                  }}
                >
                  <span
                    style={{
                      flex: 1,
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {space.name}
                  </span>
                  {space.id === currentSpaceId && (
                    <span
                      style={{
                        width: 6,
                        height: 6,
                        borderRadius: "50%",
                        background: "#0ea5e9",
                        flexShrink: 0,
                      }}
                    />
                  )}
                </button>
              ))}

              <div
                style={{
                  borderTop: "1px solid var(--sidebar-section-border)",
                  margin: "4px 0",
                }}
              />

              <button
                onClick={handleCreateNew}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 6,
                  width: "100%",
                  padding: "8px 10px",
                  borderRadius: 6,
                  border: "none",
                  background: "transparent",
                  color: "#0ea5e9",
                  fontSize: 12,
                  fontWeight: 500,
                  cursor: "pointer",
                  textAlign: "start",
                }}
              >
                <span>+</span>
                <span>{t("createNew")}</span>
              </button>
            </>
          )}
        </div>
      )}
    </div>
  );
}
