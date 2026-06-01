"use client";

import { useRef, useState, useCallback, useEffect } from "react";
import SubmissionModal from "@/components/shell/SubmissionModal";

type SubmissionType = "bug" | "feedback";

/** Event detail for programmatically opening the feedback modal */
export interface OpenFeedbackEvent {
  type: SubmissionType;
  initialDescription?: string;
}

/**
 * Opens the feedback/bug report modal programmatically from anywhere.
 * Dispatches a custom event that FeedbackFab listens to.
 */
export function openFeedbackModal(detail: OpenFeedbackEvent) {
  window.dispatchEvent(new CustomEvent("open-feedback-modal", { detail }));
}

/**
 * FeedbackFab — A fixed-position split FAB for bug reports and feedback.
 * Left half: bug icon, Right half: feedback icon.
 * Clicking either half opens the SubmissionModal with the appropriate type.
 */
export default function FeedbackFab() {
  const [modalOpen, setModalOpen] = useState(false);
  const [submissionType, setSubmissionType] = useState<SubmissionType | null>(null);
  const [initialDescription, setInitialDescription] = useState<string | undefined>(undefined);

  // Refs for focus restoration
  const bugButtonRef = useRef<HTMLButtonElement>(null);
  const feedbackButtonRef = useRef<HTMLButtonElement>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);

  const handleClick = useCallback((type: SubmissionType) => {
    const ref = type === "bug" ? bugButtonRef : feedbackButtonRef;
    triggerRef.current = ref.current;
    setSubmissionType(type);
    setInitialDescription(undefined);
    setModalOpen(true);
  }, []);

  const handleClose = useCallback(() => {
    setModalOpen(false);
    setSubmissionType(null);
    setInitialDescription(undefined);
  }, []);

  // Listen for programmatic open events (e.g. from solver error banner)
  useEffect(() => {
    function handleOpenEvent(e: Event) {
      const detail = (e as CustomEvent<OpenFeedbackEvent>).detail;
      triggerRef.current = bugButtonRef.current;
      setSubmissionType(detail.type);
      setInitialDescription(detail.initialDescription);
      setModalOpen(true);
    }
    window.addEventListener("open-feedback-modal", handleOpenEvent);
    return () => window.removeEventListener("open-feedback-modal", handleOpenEvent);
  }, []);

  return (
    <>
      <div
        className="fixed z-[1200] flex items-stretch opacity-40 hover:opacity-100 transition-opacity"
        style={{ bottom: 72, insetInlineStart: 12 }}
      >
        {/* Bug report half (left) */}
        <button
          ref={bugButtonRef}
          type="button"
          aria-label="Report a bug"
          onClick={() => handleClick("bug")}
          className="flex items-center justify-center w-9 h-9 rounded-l-lg bg-slate-800 hover:bg-slate-700 text-white transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 focus-visible:ring-offset-2"
        >
          {/* Bug icon */}
          <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth={1.8}
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M8 2l1.88 1.88M14.12 3.88L16 2" />
            <path d="M9 7.13v-1a3.003 3.003 0 116 0v1" />
            <path d="M12 20c-3.3 0-6-2.7-6-6v-3a6 6 0 0112 0v3c0 3.3-2.7 6-6 6z" />
            <path d="M12 20v-9" />
            <path d="M6.53 9C4.6 8.8 3 7.1 3 5" />
            <path d="M6 13H2" />
            <path d="M3 21c0-2.1 1.7-3.9 3.8-4" />
            <path d="M20.97 5c0 2.1-1.6 3.8-3.5 4" />
            <path d="M22 13h-4" />
            <path d="M17.2 17c2.1.1 3.8 1.9 3.8 4" />
          </svg>
        </button>

        {/* Visual divider */}
        <div className="w-px bg-slate-600" aria-hidden="true" />

        {/* Feedback half (right) */}
        <button
          ref={feedbackButtonRef}
          type="button"
          aria-label="Submit feedback"
          onClick={() => handleClick("feedback")}
          className="flex items-center justify-center w-9 h-9 rounded-r-lg bg-slate-800 hover:bg-slate-700 text-white transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 focus-visible:ring-offset-2"
        >
          {/* Feedback / message icon */}
          <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth={1.8}
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z" />
            <path d="M8 9h8" />
            <path d="M8 13h6" />
          </svg>
        </button>
      </div>

      {/* Submission modal — rendered conditionally */}
      {modalOpen && submissionType && (
        <SubmissionModal
          open={modalOpen}
          submissionType={submissionType}
          onClose={handleClose}
          triggerRef={triggerRef}
          initialDescription={initialDescription}
        />
      )}
    </>
  );
}
